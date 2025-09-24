// Janus VideoRoom JavaScript Interop
window.janusInterop = {
    // Internal state
    _dotNetRef: null,
    _janusSettings: null,
    _ws: null,
    _sessionId: null,
    _publisherHandle: null,
    _subscriberHandles: new Map(), // feedId -> handleId
    _publisherPc: null,
    _subscriberPcs: new Map(), // feedId -> RTCPeerConnection
    _localStream: null,
    _pendingTransactions: new Map(), // transaction -> {resolve, reject}
    _eventWaiters: [],
    _keepAliveTimer: null,
    _roomId: null,
    _displayName: null,
    _isConnected: false,
    _isInRoom: false,
    _isScreenSharing: false,
    _currentConstraints: null,
    _processedStreams: new Set(),
    // Initialize with .NET reference and settings
    initialize: function(dotNetRef, janusSettings) {
        this._dotNetRef = dotNetRef;
        this._janusSettings = janusSettings;
        this._log("Janus interop initialized", "info");
    },

    // Get available media devices
    getMediaDevices: async function() {
        try {
            // Firefox için explicit permission request
            let permissionGranted = false;
            try {
                // Küçük bir test stream ile permission iste
                const testStream = await navigator.mediaDevices.getUserMedia({
                    audio: true,
                    video: true
                });
                testStream.getTracks().forEach(track => track.stop());
                permissionGranted = true;
                this._log("Media permissions granted", "ok");
            } catch (permError) {
                this._log(`Permission denied or not available: ${permError.message}`, "warn");
                // İzin verilmese bile device listesini almaya çalış
            }

            const devices = await navigator.mediaDevices.enumerateDevices();
            const deviceList = devices.map(d => ({
                deviceId: d.deviceId,
                label: d.label || (permissionGranted ?
                        `Device ${d.deviceId.substring(0, 4)}...` :
                        `${d.kind} (permission needed)`
                ),
                kind: d.kind
            }));

            return JSON.stringify(deviceList);
        } catch (error) {
            console.error("Failed to enumerate devices:", error);
            this._log(`Failed to get devices: ${error.message}`, "error");
            return JSON.stringify([]);
        }
    },

    // Connect to Janus WebSocket server
    connect: function(wsUrl, roomId, displayName) {
        return new Promise((resolve, reject) => {
            try {
                this._roomId = roomId;
                this._displayName = displayName;

                this._log(`Connecting to WebSocket: ${wsUrl}`, "info");
                this._ws = new WebSocket(wsUrl, 'janus-protocol');

                this._ws.onopen = async () => {
                    try {
                        this._log("WebSocket connected", "ok");

                        // Create Janus session
                        const createResp = await this._sendJanusMessage({ janus: "create" });
                        this._sessionId = createResp.data.id;
                        this._log(`Janus session created: ${this._sessionId}`, "ok");

                        // Attach to videoroom plugin
                        const attachResp = await this._sendJanusMessage({
                            janus: "attach",
                            session_id: this._sessionId,
                            plugin: "janus.plugin.videoroom"
                        });
                        this._publisherHandle = attachResp.data.id;
                        this._log(`Publisher handle attached: ${this._publisherHandle}`, "ok");

                        // Start keepalive
                        this._keepAliveTimer = setInterval(() => {
                            this._keepAlive().catch(err => this._log(`Keepalive failed: ${err.message}`, "warn"));
                        }, 25000);

                        this._isConnected = true;
                        await this._notifyConnectionState("connected");
                        resolve();
                    } catch (error) {
                        this._log(`Connection setup failed: ${error.message}`, "error");
                        reject(error);
                    }
                };

                this._ws.onerror = (error) => {
                    this._log(`WebSocket error: ${error}`, "error");
                    this._notifyConnectionState("error");
                };

                this._ws.onclose = () => {
                    this._log("WebSocket closed", "warn");
                    this._cleanup();
                    this._notifyConnectionState("disconnected");
                };

                this._ws.onmessage = (event) => {
                    this._handleJanusMessage(event);
                };

            } catch (error) {
                this._log(`Connect failed: ${error.message}`, "error");
                reject(error);
            }
        });
    },

    // Join room and start publishing
    // Join room and start publishing
    joinAndPublish: async function(constraintsJson) {
        try {
            if (!this._isConnected) {
                throw new Error("Not connected to Janus server");
            }

            this._currentConstraints = constraintsJson !== "null" ? JSON.parse(constraintsJson) : null;

            // Create/join room
            await this._createRoom();

            // Join as publisher
            await this._sendJanusMessage({
                janus: "message",
                session_id: this._sessionId,
                handle_id: this._publisherHandle,
                body: {
                    request: "join",
                    ptype: "publisher",
                    room: parseInt(this._roomId),
                    display: this._displayName
                }
            });

            // Wait for joined event
            const joinedEvent = await this._waitForEvent(msg =>
                msg.janus === "event" &&
                msg.sender === this._publisherHandle &&
                msg.plugindata?.plugin === "janus.plugin.videoroom" &&
                msg.plugindata?.data?.videoroom === "joined"
            );

            this._log(`Joined room as publisher (id=${joinedEvent.plugindata.data.id})`, "ok");

            // Get user media
            await this._setupLocalStream();

            // Setup publisher peer connection
            await this._setupPublisherPeerConnection();

            // Subscribe to existing publishers
            const existingPublishers = joinedEvent.plugindata.data.publishers;
            if (Array.isArray(existingPublishers)) {
                for (const publisher of existingPublishers) {
                    await this._subscribeToFeed(publisher.id);
                }
            }

            this._isInRoom = true;
            this._log("Join and publish completed", "ok");

            // BUNU EKLE - Success return et
            return "success";

        } catch (error) {
            this._log(`Join and publish failed: ${error.message}`, "error");
            throw error;
        }
    },
    
    // Switch camera during call
    switchCamera: async function(deviceId) {
        try {
            if (!this._isInRoom || this._isScreenSharing) {
                throw new Error("Cannot switch camera in current state");
            }

            this._log(`Switching camera to: ${deviceId}`, "info");

            const videoTrack = this._localStream.getVideoTracks()[0];
            if (videoTrack) {
                videoTrack.stop();
            }

            // Get new video stream
            const constraints = {
                video: deviceId ? { deviceId: { exact: deviceId } } : { width: 1280, height: 720 },
                audio: false
            };

            const newStream = await navigator.mediaDevices.getUserMedia(constraints);
            const newVideoTrack = newStream.getVideoTracks()[0];

            // Replace video track in peer connection
            const sender = this._publisherPc.getSenders().find(s =>
                s.track && s.track.kind === 'video'
            );

            if (sender) {
                await sender.replaceTrack(newVideoTrack);
            }

            // Update local stream
            this._localStream.removeTrack(videoTrack);
            this._localStream.addTrack(newVideoTrack);

            // Update local video element
            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = this._localStream;
            }
            await this._notifyDeviceChanged("camera");
            this._log("Camera switched successfully", "ok");

        } catch (error) {
            this._log(`Camera switch failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Switch microphone during call
    switchMicrophone: async function(deviceId) {
        try {
            if (!this._isInRoom) {
                throw new Error("Cannot switch microphone when not in room");
            }

            this._log(`Switching microphone to: ${deviceId}`, "info");

            const audioTrack = this._localStream.getAudioTracks()[0];
            if (audioTrack) {
                audioTrack.stop();
            }

            // Get new audio stream
            const constraints = {
                audio: deviceId ? { deviceId: { exact: deviceId } } : true,
                video: false
            };

            const newStream = await navigator.mediaDevices.getUserMedia(constraints);
            const newAudioTrack = newStream.getAudioTracks()[0];

            // Replace audio track in peer connection
            const sender = this._publisherPc.getSenders().find(s =>
                s.track && s.track.kind === 'audio'
            );

            if (sender) {
                await sender.replaceTrack(newAudioTrack);
            }

            // Update local stream
            this._localStream.removeTrack(audioTrack);
            this._localStream.addTrack(newAudioTrack);

            await this._notifyDeviceChanged("microphone");
            this._log("Microphone switched successfully", "ok");

        } catch (error) {
            this._log(`Microphone switch failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Start screen sharing
    startScreenShare: async function() {
        try {
            if (!this._isInRoom || this._isScreenSharing) {
                throw new Error("Cannot start screen share in current state");
            }

            this._log("Starting screen share", "info");

            // Get screen stream
            const screenStream = await navigator.mediaDevices.getDisplayMedia({
                video: { frameRate: { ideal: 30 } },
                audio: false
            });

            const screenTrack = screenStream.getVideoTracks()[0];

            // Handle screen share end
            screenTrack.onended = () => {
                this.stopScreenShare().catch(err =>
                    this._log(`Screen share auto-stop failed: ${err.message}`, "error")
                );
            };

            // Replace video track
            const videoSender = this._publisherPc.getSenders().find(s =>
                s.track && s.track.kind === 'video'
            );

            if (videoSender) {
                await videoSender.replaceTrack(screenTrack);
            }

            // Update local stream
            const oldVideoTrack = this._localStream.getVideoTracks()[0];
            if (oldVideoTrack) {
                this._localStream.removeTrack(oldVideoTrack);
                oldVideoTrack.stop();
            }
            this._localStream.addTrack(screenTrack);
            // Update local video element with screen stream
            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = this._localStream;
                this._log("Local video updated with screen share", "ok");
            }

            this._isScreenSharing = true;
            await this._notifyScreenShareStarted();
            this._log("Screen share started successfully", "ok");

        } catch (error) {
            this._log(`Screen share start failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Stop screen sharing
    stopScreenShare: async function() {
        try {
            if (!this._isScreenSharing) {
                throw new Error("Screen sharing not active");
            }

            this._log("Stopping screen share", "info");

            // Get camera stream back
            const videoConstraints = this._currentConstraints?.video?.deviceId
                ? { deviceId: { exact: this._currentConstraints.video.deviceId } }
                : { width: 1280, height: 720 };

            const cameraStream = await navigator.mediaDevices.getUserMedia({
                video: videoConstraints,
                audio: false
            });
             
            const cameraTrack = cameraStream.getVideoTracks()[0];

            // Replace screen track with camera track
            const videoSender = this._publisherPc.getSenders().find(s =>
                s.track && s.track.kind === 'video'
            );

            if (videoSender) {
                await videoSender.replaceTrack(cameraTrack);
            }

            // Update local stream
            const screenTrack = this._localStream.getVideoTracks()[0];
            if (screenTrack) {
                this._localStream.removeTrack(screenTrack);
                screenTrack.stop();
            }
            this._localStream.addTrack(cameraTrack);
            // Update local video element with camera stream
            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = this._localStream;
                this._log("Local video updated with camera", "ok");
            }

            this._isScreenSharing = false;
            await this._notifyScreenShareStopped();
            this._log("Screen share stopped successfully", "ok");

        } catch (error) {
            this._log(`Screen share stop failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Leave room
    leaveRoom: async function() {
        try {
            if (!this._isInRoom) {
                return;
            }

            this._log("Leaving room", "info");

            // Send leave message
            if (this._publisherHandle) {
                await this._sendJanusMessage({
                    janus: "message",
                    session_id: this._sessionId,
                    handle_id: this._publisherHandle,
                    body: { request: "leave" }
                });
            }

            // Cleanup streams and connections
            this._cleanupStreamsAndConnections();

            this._isInRoom = false;
            this._isScreenSharing = false;
            this._log("Left room successfully", "ok");

        } catch (error) {
            this._log(`Leave room failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Disconnect from Janus
    disconnect: async function() {
        try {
            this._log("Disconnecting from Janus", "info");

            if (this._keepAliveTimer) {
                clearInterval(this._keepAliveTimer);
                this._keepAliveTimer = null;
            }

            if (this._ws && this._ws.readyState === WebSocket.OPEN) {
                this._ws.close();
            }

            this._cleanup();
            this._log("Disconnected from Janus", "ok");

        } catch (error) {
            this._log(`Disconnect failed: ${error.message}`, "error");
            throw error;
        }
    },

    // Private helper methods
    _log: function(message, level = "info") {
        console.log(`[Janus] ${message}`);
        if (this._dotNetRef) {
            this._dotNetRef.invokeMethodAsync('HandleLogMessage', message, level)
                .catch(err => console.error('Failed to invoke HandleLogMessage:', err));
        }
    },

    _generateTransaction: function() {
        return Math.random().toString(36).substring(2, 15);
    },

    _sendJanusMessage: function(message, timeoutMs = 10000) {
        return new Promise((resolve, reject) => {
            const transaction = message.transaction || this._generateTransaction();
            message.transaction = transaction;

            this._pendingTransactions.set(transaction, { resolve, reject });

            this._ws.send(JSON.stringify(message));

            setTimeout(() => {
                if (this._pendingTransactions.has(transaction)) {
                    this._pendingTransactions.delete(transaction);
                    reject(new Error(`Transaction timeout: ${transaction}`));
                }
            }, timeoutMs);
        });
    },

    _waitForEvent: function(matchFunction, timeoutMs = 10000) {
        return new Promise((resolve, reject) => {
            const waiter = {
                match: matchFunction,
                resolve,
                reject,
                expireAt: Date.now() + timeoutMs
            };

            this._eventWaiters.push(waiter);

            setTimeout(() => {
                const index = this._eventWaiters.indexOf(waiter);
                if (index >= 0) {
                    this._eventWaiters.splice(index, 1);
                    reject(new Error("Event wait timeout"));
                }
            }, timeoutMs + 50);
        });
    },

    _handleJanusMessage: async function(event) {
        try {
            const message = JSON.parse(event.data);
            const transaction = message.transaction;

            // Handle pending transactions
            if (transaction && this._pendingTransactions.has(transaction)) {
                const { resolve } = this._pendingTransactions.get(transaction);
                this._pendingTransactions.delete(transaction);
                resolve(message);
                return;
            }

            // Handle event waiters
            if (this._eventWaiters.length > 0) {
                for (let i = 0; i < this._eventWaiters.length; i++) {
                    const waiter = this._eventWaiters[i];
                    if (waiter.match(message)) {
                        this._eventWaiters.splice(i, 1);
                        waiter.resolve(message);
                        return;
                    }
                }
            }

            // Handle specific events
            await this._handleSpecificEvents(message);

        } catch (error) {
            this._log(`Message handling failed: ${error.message}`, "error");
        }
    },

    _handleSpecificEvents: async function(message) {
        if (message.janus === "event" && message.plugindata?.plugin === "janus.plugin.videoroom") {
            const data = message.plugindata.data;

            // Handle JSEP (WebRTC signaling)
            if (message.jsep) {
                await this._handleJsep(message);
            }

            // Handle new publishers
            if (data?.videoroom === "event" && Array.isArray(data.publishers)) {
                for (const publisher of data.publishers) {
                    await this._subscribeToFeed(publisher.id);
                }
            }

            // Handle leaving publisher
            if (data?.videoroom === "event" && data.leaving) {
                await this._handlePublisherLeft(data.leaving);
            }
        }
    },

    _handleJsep: async function(message) {
        const jsep = message.jsep;

        if (jsep.type === "answer" && this._publisherPc) {
            // Publisher answer
            await this._publisherPc.setRemoteDescription(jsep);
            this._log("Publisher remote answer set", "ok");
        } else if (jsep.type === "offer") {
            // Subscriber offer
            const feedId = this._findFeedIdByHandle(message.sender);
            const pc = this._subscriberPcs.get(feedId);

            if (pc) {
                await pc.setRemoteDescription(jsep);
                const answer = await pc.createAnswer();
                await pc.setLocalDescription(answer);

                await this._sendJanusMessage({
                    janus: "message",
                    session_id: this._sessionId,
                    handle_id: message.sender,
                    body: { request: "start", room: parseInt(this._roomId) },
                    jsep: { type: "answer", sdp: answer.sdp }
                });

                this._log(`Subscriber answered for feed ${feedId}`, "ok");
            }
        }
    },

    _createRoom: async function() {
        const body = {
            request: "create",
            room: parseInt(this._roomId),
            publishers: 12,
            description: "Blazor Video Chat Room"
        };

        if (this._janusSettings.adminSecret) {
            body.admin_key = this._janusSettings.adminSecret;
        }

        try {
            const response = await this._sendJanusMessage({
                janus: "message",
                session_id: this._sessionId,
                handle_id: this._publisherHandle,
                body: body
            });

            const data = response.plugindata?.data;
            if (data?.videoroom === "created" ||
                (data?.videoroom === "event" && data?.room === parseInt(this._roomId))) {
                this._log(`Room ${this._roomId} created/exists`, "ok");
            }
        } catch (error) {
            // Room might already exist, that's okay
            this._log(`Room creation response: ${error.message}`, "warn");
        }
    },

    _setupLocalStream: async function() {
        try {
            let constraints;

            if (this._currentConstraints) {
                constraints = {
                    audio: this._currentConstraints.audio || true,
                    video: this._currentConstraints.video || { width: 1280, height: 720 }
                };
            } else {
                constraints = {
                    audio: true,
                    video: { width: 1280, height: 720 }
                };
            }

            this._localStream = await navigator.mediaDevices.getUserMedia(constraints);
            // Assign stream to local video element
            const localVideo = document.getElementById('localVideo');
            if (localVideo) {
                localVideo.srcObject = this._localStream;
                localVideo.muted = true; // Prevent echo
                this._log("Local video element updated", "ok");
            }

            if (this._dotNetRef) {
                await this._dotNetRef.invokeMethodAsync('HandleLocalStreamReady');
            }

            this._log("Local stream setup completed", "ok");
        } catch (error) {
            this._log(`Local stream setup failed: ${error.message}`, "error");
            throw error;
        }
    },

    _setupPublisherPeerConnection: async function() {
        this._publisherPc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
        });

        // Add local stream tracks
        this._localStream.getTracks().forEach(track => {
            this._publisherPc.addTrack(track, this._localStream);
        });

        // Handle ICE candidates
        this._publisherPc.onicecandidate = (event) => {
            if (event.candidate) {
                this._ws.send(JSON.stringify({
                    janus: "trickle",
                    session_id: this._sessionId,
                    handle_id: this._publisherHandle,
                    candidate: {
                        candidate: event.candidate.candidate,
                        sdpMid: event.candidate.sdpMid,
                        sdpMLineIndex: event.candidate.sdpMLineIndex
                    },
                    transaction: this._generateTransaction()
                }));
            }
        };

        // Create and send offer
        const offer = await this._publisherPc.createOffer();
        await this._publisherPc.setLocalDescription(offer);

        await this._sendJanusMessage({
            janus: "message",
            session_id: this._sessionId,
            handle_id: this._publisherHandle,
            body: { request: "publish", audio: true, video: true },
            jsep: { type: "offer", sdp: offer.sdp }
        });

        this._log("Publisher offer sent", "ok");
    },

    _subscribeToFeed: async function(feedId) {
        try {
            // Attach new handle for subscriber
            const attachResp = await this._sendJanusMessage({
                janus: "attach",
                session_id: this._sessionId,
                plugin: "janus.plugin.videoroom"
            });

            const subscriberHandle = attachResp.data.id;
            this._subscriberHandles.set(feedId, subscriberHandle);

            // Join as subscriber
            await this._sendJanusMessage({
                janus: "message",
                session_id: this._sessionId,
                handle_id: subscriberHandle,
                body: {
                    request: "join",
                    ptype: "subscriber",
                    room: parseInt(this._roomId),
                    feed: feedId,
                    close_pc: true
                }
            });

            // Setup peer connection
            const pc = new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            });

            this._subscriberPcs.set(feedId, pc);

            pc.ontrack = (event) => {
                // Sadece ilk stream event'ini işle, duplikatları önle
                if (!this._processedStreams) this._processedStreams = new Set();
                const streamId = event.streams[0].id;

                if (!this._processedStreams.has(streamId)) {
                    this._processedStreams.add(streamId);
                    this._handleRemoteStream(feedId, event.streams[0]);
                }
            };

            pc.onicecandidate = (event) => {
                if (event.candidate) {
                    this._ws.send(JSON.stringify({
                        janus: "trickle",
                        session_id: this._sessionId,
                        handle_id: subscriberHandle,
                        candidate: {
                            candidate: event.candidate.candidate,
                            sdpMid: event.candidate.sdpMid,
                            sdpMLineIndex: event.candidate.sdpMLineIndex
                        },
                        transaction: this._generateTransaction()
                    }));
                }
            };

            this._log(`Subscribed to feed ${feedId}`, "ok");

        } catch (error) {
            this._log(`Failed to subscribe to feed ${feedId}: ${error.message}`, "error");
        }
    },

    _handleRemoteStream: async function(feedId, stream) {
        try {
            const participant = {
                userId: feedId.toString(),
                displayName: `Participant ${feedId}`,
                avatarUrl: "",
                joinedAt: new Date().toISOString(),
                isMuted: false,
                isVideoEnabled: stream.getVideoTracks().length > 0,
                isAudioEnabled: stream.getAudioTracks().length > 0,
                isScreenSharing: false,
                role: 0, // Participant
                mediaSettings: {}
            };

            if (this._dotNetRef) {
                await this._dotNetRef.invokeMethodAsync('HandleRemoteStreamAdded', JSON.stringify(participant));
            }
            // Assign remote stream to video element
            const remoteVideo = document.getElementById(`remote-${feedId}`);
            if (remoteVideo) {
                remoteVideo.srcObject = stream;
                this._log(`Remote video element updated for feed ${feedId}`, "ok");
            }

            this._log(`Remote stream added for feed ${feedId}`, "ok");
        } catch (error) {
            this._log(`Failed to handle remote stream for feed ${feedId}: ${error.message}`, "error");
        }
    },

    _handlePublisherLeft: async function(feedId) {
        try {
            // Cleanup subscriber resources
            const pc = this._subscriberPcs.get(feedId);
            if (pc) {
                pc.close();
                this._subscriberPcs.delete(feedId);
            }

            this._subscriberHandles.delete(feedId);

            // Remove video element from DOM
            const videoElement = document.getElementById(`remote-${feedId}`);
            if (videoElement) {
                videoElement.remove();
                this._log(`Removed video element for feed ${feedId}`, "ok");
            }

            if (this._dotNetRef) {
                try {
                    await this._dotNetRef.invokeMethodAsync('HandleRemoteStreamRemoved', feedId);
                } catch (err) {
                    // Component disposed olmuş olabilir, ignore et
                    this._log(`DotNet callback failed for feed ${feedId} (component may be disposed)`, "warn");
                }
            }

            this._log(`Publisher ${feedId} left`, "warn");
        } catch (error) {
            this._log(`Failed to handle publisher left ${feedId}: ${error.message}`, "error");
        }
    },
    _findFeedIdByHandle: function(handleId) {
        for (const [feedId, handle] of this._subscriberHandles.entries()) {
            if (handle === handleId) {
                return feedId;
            }
        }
        return null;
    },

    _keepAlive: async function() {
        if (!this._sessionId) return;

        await this._sendJanusMessage({
            janus: "keepalive",
            session_id: this._sessionId
        });

        this._log("Keepalive sent", "ok");
    },

    _cleanupStreamsAndConnections: function() {
        // Stop local stream
        if (this._localStream) {
            this._localStream.getTracks().forEach(track => track.stop());
            this._localStream = null;
        }

        // Close publisher peer connection
        if (this._publisherPc) {
            this._publisherPc.close();
            this._publisherPc = null;
        }

        // Close subscriber peer connections
        for (const pc of this._subscriberPcs.values()) {
            pc.close();
        }
        this._subscriberPcs.clear();
        this._subscriberHandles.clear();
    },

    _cleanup: function() {
        this._cleanupStreamsAndConnections();

        this._sessionId = null;
        this._publisherHandle = null;
        this._isConnected = false;
        this._isInRoom = false;
        this._isScreenSharing = false;
        // Clear processed streams
        if (this._processedStreams) {
            this._processedStreams.clear();
        }
        this._eventWaiters.length = 0;

        if (this._keepAliveTimer) {
            clearInterval(this._keepAliveTimer);
            this._keepAliveTimer = null;
        }
    },

    // Notification helpers
    _notifyConnectionState: async function(state) {
        if (this._dotNetRef) {
            await this._dotNetRef.invokeMethodAsync('HandleConnectionStateChanged', state);
        }
    },

    _notifyDeviceChanged: async function(deviceType) {
        if (this._dotNetRef) {
            await this._dotNetRef.invokeMethodAsync('HandleDeviceChanged', deviceType);
        }
    },

    _notifyScreenShareStarted: async function() {
        if (this._dotNetRef) {
            await this._dotNetRef.invokeMethodAsync('HandleScreenShareStarted');
        }
    },

    _notifyScreenShareStopped: async function() {
        if (this._dotNetRef) {
            await this._dotNetRef.invokeMethodAsync('HandleScreenShareStopped');
        }
    }
};