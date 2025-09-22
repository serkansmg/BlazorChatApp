
export class JanusVideoClient {
  /**
   * @param {object} opts
   * @param {string} opts.wsUrl
   * @param {number|string} opts.roomId
   * @param {string} [opts.display]
   * @param {(msg:string, level?:"ok"|"warn"|"err")=>void} [opts.onLog]
   * @param {(stream:MediaStream)=>void} [opts.onLocalStream]
   * @param {(feedId:number, stream:MediaStream)=>void} [opts.onRemoteStream]
   * @param {(feedId:number)=>void} [opts.onRemoteLeft]
   */
  constructor(opts) {
    this.wsUrl   = opts.wsUrl;
    this.room    = Number(opts.roomId);
    this.display = opts.display || ("user-" + Math.random().toString(36).slice(2,7));
    this.onLog   = typeof opts.onLog === "function" ? opts.onLog : () => {};
    this.onLocal = typeof opts.onLocalStream === "function" ? opts.onLocalStream : () => {};
    this.onRemote= typeof opts.onRemoteStream === "function" ? opts.onRemoteStream : () => {};
    this.onRemoteLeft = typeof opts.onRemoteLeft === "function" ? opts.onRemoteLeft : () => {};

    // WS/Janus
    this.ws = null;
    this.sessionId = null;
    this.pubHandle = null;
    this.pending = new Map(); // tx -> {resolve,reject}
    this.waiters = [];        // {match,resolve,reject,expireAt}
    this.keepaliveTimer = null;

    // Feeds/PCs
    this.subHandles = new Map(); // feedId -> handleId
    this.pubPc = null;
    this.subPcs = new Map();     // feedId -> RTCPeerConnection
    this.localStream = null;

    // Media selections
    this.selectedAudioInputId = null;
    this.selectedVideoInputId = null;
    this.selectedAudioOutputId = null; // sink (setSinkId destekleyenlerde)
    this.useScreenShare = false;
  }

  // ---------- helpers ----------
  _log(m,l){ this.onLog(m,l); }
  _tx(){ return Math.random().toString(36).slice(2); }

  _send(obj, timeoutMs=10000){
    const tx = obj.transaction || this._tx();
    obj.transaction = tx;
    return new Promise((resolve,reject)=>{
      this.pending.set(tx,{resolve,reject});
      this.ws.send(JSON.stringify(obj));
      setTimeout(()=>{
        if(this.pending.has(tx)){
          this.pending.delete(tx);
          reject(new Error("timeout:"+tx));
        }
      }, timeoutMs);
    });
  }

  _waitEvent(matchFn, timeoutMs=10000){
    return new Promise((resolve,reject)=>{
      const waiter = {match:matchFn, resolve, reject, expireAt: Date.now()+timeoutMs};
      this.waiters.push(waiter);
      setTimeout(()=>{
        const i = this.waiters.indexOf(waiter);
        if(i>=0){ this.waiters.splice(i,1); reject(new Error("event timeout")); }
      }, timeoutMs+50);
    });
  }

  // ---------- public: device utils ----------
  static async listDevices() {
    // Not: label’ların dolu gelmesi için tarayıcıya en az bir kez izin vermek gerekir.
    const devices = await navigator.mediaDevices.enumerateDevices();
    return devices.map(d=>({ deviceId: d.deviceId, label: d.label, kind: d.kind }));
  }

  setSelections({ audioInputId=null, videoInputId=null, audioOutputId=null, screenShare=false } = {}) {
    this.selectedAudioInputId = audioInputId || null;
    this.selectedVideoInputId = videoInputId || null;
    this.selectedAudioOutputId= audioOutputId || null;
    this.useScreenShare = !!screenShare;
    this._log(`selections: audioIn=${this.selectedAudioInputId||"-"}, videoIn=${this.selectedVideoInputId||"-"}, out=${this.selectedAudioOutputId||"-"}, screen=${this.useScreenShare}`);
  }

  async applyAudioSink(videoElement) {
    // Sadece Chrome/Edge destekler: HTMLMediaElement.setSinkId
    if (!videoElement || typeof videoElement.setSinkId !== 'function' || !this.selectedAudioOutputId) return;
    try{
      await videoElement.setSinkId(this.selectedAudioOutputId);
      this._log(`audio sink set: ${this.selectedAudioOutputId}`, "ok");
    }catch(e){
      this._log(`audio sink set failed: ${e.message}`,"warn");
    }
  }

  // ---------- connect ----------
  async connect(){
    return new Promise((resolve,reject)=>{
      this._log(`WS connect: ${this.wsUrl}`);
      this.ws = new WebSocket(this.wsUrl, 'janus-protocol');
      this.ws.onopen = async ()=>{
        this._log("WS OPEN","ok");
        try{
          const create = await this._send({janus:"create"});
          this.sessionId = create.data.id;
          this._log("session: "+this.sessionId, "ok");

          const attach = await this._send({ janus:"attach", session_id:this.sessionId, plugin:"janus.plugin.videoroom" });
          this.pubHandle = attach.data.id;
          this._log("publisher handle: "+this.pubHandle, "ok");

          this.keepaliveTimer = setInterval(()=> this.keepAlive().catch(()=>{}), 25000);
          resolve();
        }catch(e){ reject(e); }
      };
      this.ws.onerror = ()=> this._log("WS ERROR","err");
      this.ws.onclose = ()=> this._log("WS CLOSED","warn");
      this.ws.onmessage = (ev)=> this._onMessage(ev);
    });
  }

  async keepAlive(){
    if(!this.sessionId) return;
    await this._send({janus:"keepalive", session_id:this.sessionId});
    this._log("keepalive ok","ok");
  }

  // ---------- room ops ----------
  async createRoom(adminKey){
    if(!this.pubHandle) throw new Error("no publisher handle");
    const body = { request:"create", room:this.room, publishers:12, description:"demo room" };
    if (adminKey) body.admin_key = adminKey;
    const res = await this._send({ janus:"message", session_id:this.sessionId, handle_id:this.pubHandle, body });
    const data = res.plugindata?.data;
    if (data?.videoroom === "created" || (data?.videoroom==="event" && data?.room===this.room)) {
      this._log("room created/exists: "+this.room,"ok");
    } else {
      this._log("create resp: "+JSON.stringify(data),"warn");
    }
  }

  async joinAndPublish(localPreviewEl=null){
    // join (ACK dönebilir, gerçek 'joined' event'ini bekle)
    await this._send({ janus:"message", session_id:this.sessionId, handle_id:this.pubHandle,
      body:{ request:"join", ptype:"publisher", room:this.room, display:this.display } });

    const joinedEvt = await this._waitEvent(m =>
      m.janus==="event" && m.sender===this.pubHandle &&
      m.plugindata?.plugin==="janus.plugin.videoroom" &&
      m.plugindata?.data?.videoroom==="joined"
    , 8000);
    const joinedData = joinedEvt.plugindata.data;
    this._log(`joined as publisher (id=${joinedData.id})`,"ok");

    // capture
    if (this.useScreenShare) {
      // Ekran + (mümkünse) mikrofon
      const screen = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: { ideal: 30 } }, audio: false });
      let mic = null;
      if (this.selectedAudioInputId) {
        try {
          mic = await navigator.mediaDevices.getUserMedia({ audio: { deviceId: { exact: this.selectedAudioInputId } }, video: false });
        } catch(_) { this._log("mic capture failed (screenshare)","warn"); }
      }
      // Ekran videosu + (opsiyonel) mikrofonu birleştir
      const mixed = new MediaStream();
      screen.getVideoTracks().forEach(t => mixed.addTrack(t));
      if (mic) mic.getAudioTracks().forEach(t => mixed.addTrack(t));
      this.localStream = mixed;
    } else {
      const constraints = {
        audio: this.selectedAudioInputId ? { deviceId: { exact: this.selectedAudioInputId } } : true,
        video: this.selectedVideoInputId ? { deviceId: { exact: this.selectedVideoInputId } } : { width: 1280, height: 720 }
      };
      this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
    }

    // yerel önizleme
    if (localPreviewEl) { localPreviewEl.srcObject = this.localStream; localPreviewEl.muted = true; }

    // publisher PC
    this.pubPc = new RTCPeerConnection({ iceServers:[{urls:'stun:stun.l.google.com:19302'}] });
    this.localStream.getTracks().forEach(t => this.pubPc.addTrack(t, this.localStream));
    this.pubPc.onicecandidate = (e)=>{
      if(e.candidate){
        this.ws.send(JSON.stringify({
          janus:"trickle", session_id:this.sessionId, handle_id:this.pubHandle,
          candidate:{ candidate:e.candidate.candidate, sdpMid:e.candidate.sdpMid, sdpMLineIndex:e.candidate.sdpMLineIndex },
          transaction:this._tx()
        }));
      }
    };

    const offer = await this.pubPc.createOffer();
    await this.pubPc.setLocalDescription(offer);

    await this._send({
      janus:"message", session_id:this.sessionId, handle_id:this.pubHandle,
      body:{ request:"publish", audio:true, video:true },
      jsep:{ type:"offer", sdp:offer.sdp }
    });
    this._log("publish sent","ok");

    // var olan publisher’ları subscribe et
    if (Array.isArray(joinedData.publishers)) {
      for (const p of joinedData.publishers) await this.subscribeToFeed(p.id);
    }
  }

  async subscribeToFeed(feedId){
    const attach = await this._send({ janus:"attach", session_id:this.sessionId, plugin:"janus.plugin.videoroom" });
    const subHandle = attach.data.id;
    this.subHandles.set(feedId, subHandle);
    this._log(`subscriber handle(${feedId}): ${subHandle}`);

    await this._send({
      janus:"message", session_id:this.sessionId, handle_id:subHandle,
      body:{ request:"join", ptype:"subscriber", room:this.room, feed:feedId, close_pc:true }
    });

    const pc = new RTCPeerConnection({ iceServers:[{urls:'stun:stun.l.google.com:19302'}] });
    this.subPcs.set(feedId, pc);
    pc.ontrack = (ev)=> this.onRemote(feedId, ev.streams[0]);
    pc.onicecandidate = (e)=>{
      if(e.candidate){
        this.ws.send(JSON.stringify({
          janus:"trickle", session_id:this.sessionId, handle_id:subHandle,
          candidate:{ candidate:e.candidate.candidate, sdpMid:e.candidate.sdpMid, sdpMLineIndex:e.candidate.sdpMLineIndex },
          transaction:this._tx()
        }));
      }
    };
  }

  async leave(){
    try{
      if(this.pubHandle){
        await this._send({ janus:"message", session_id:this.sessionId, handle_id:this.pubHandle, body:{request:"leave"} });
      }
    }catch(_){}
    try{ this.localStream?.getTracks().forEach(t=>t.stop()); }catch(_){}
    try{ this.pubPc?.close(); }catch(_){}
    for(const pc of this.subPcs.values()) { try{ pc.close(); }catch(_){ } }
    this.subPcs.clear();
    this._log("left room","warn");
  }

  // ---------- WS dispatcher ----------
  async _onMessage(ev){
    const msg = JSON.parse(ev.data);
    const tx = msg.transaction;

    if (tx && this.pending.has(tx)) {
      const {resolve} = this.pending.get(tx);
      this.pending.delete(tx);
      return resolve(msg);
    }

    if (this.waiters.length) {
      for (let i=0;i<this.waiters.length;i++){
        const w = this.waiters[i];
        if (w.match(msg)) { this.waiters.splice(i,1); w.resolve(msg); break; }
      }
    }

    if (msg.janus==="event" && msg.plugindata?.plugin==="janus.plugin.videoroom") {
      const data = msg.plugindata.data;

      if (msg.jsep) {
        const jsep = msg.jsep;
        if (jsep.type==="answer" && this.pubPc) {
          await this.pubPc.setRemoteDescription(jsep);
          this._log("publisher remote answer set","ok");
        } else if (jsep.type==="offer") {
          const subHandle = msg.sender;
          const feedId = [...this.subHandles.entries()].find(([,h])=>h===subHandle)?.[0];
          const pc = this.subPcs.get(feedId);
          if (!pc) return;
          await pc.setRemoteDescription(jsep);
          const answer = await pc.createAnswer();
          await pc.setLocalDescription(answer);
          await this._send({
            janus:"message", session_id:this.sessionId, handle_id:subHandle,
            body:{ request:"start", room:this.room },
            jsep:{ type:"answer", sdp:answer.sdp }
          });
          this._log(`subscriber answered (feed ${feedId})`,"ok");
        }
      }

      if (data?.videoroom==="event" && Array.isArray(data.publishers)) {
        for (const p of data.publishers) await this.subscribeToFeed(p.id);
      }

      if (data?.videoroom==="event" && data.leaving) {
        const feedId = data.leaving;
        try{ this.subPcs.get(feedId)?.close(); }catch(_){}
        this.subPcs.delete(feedId);
        this.subHandles.delete(feedId);
        this.onRemoteLeft(feedId);
        this._log(`feed ${feedId} left`,"warn");
      }
    }
  }
}

// Yardımcı (global) — cihaz listesini dışarıdan da kolay okumak için:
export async function getMediaDevices() {
  try {
    const devices = await navigator.mediaDevices.enumerateDevices();
    return devices.map(d => ({
      deviceId: d.deviceId,
      label: d.label || `Device ${d.deviceId.substring(0,4)}...`,
      kind: d.kind
    }));
  } catch (e) {
    console.error("enumerateDevices error:", e);
    return [];
  }
}
