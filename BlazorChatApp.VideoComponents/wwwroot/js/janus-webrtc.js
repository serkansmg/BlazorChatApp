let peerConnections = {};
let localStreams = {};
let mediaConstraints = {};
let dotnetRef;
let currentUserId = null;

window.initWebRTC = (dotnetInstance) => {
    dotnetRef = dotnetInstance;
    console.log('WebRTC initialized');
};

window.setMediaConstraints = (constraintsJson) => {
    try {
        mediaConstraints = JSON.parse(constraintsJson);
        console.log('Media constraints set:', mediaConstraints);
    } catch (error) {
        console.error('Error setting media constraints:', error);
    }
};

window.getMediaDevices = async () => {
    try {
        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices.map(d => ({
            deviceId: d.deviceId,
            label: d.label || `Device ${d.deviceId.substring(0, 4)}...`,
            kind: d.kind
        }));
    } catch (error) {
        console.error('Error getting media devices:', error);
        return [];
    }
};

window.getUserMediaAndPublish = async (roomId, userId, displayName) => {
    currentUserId = userId;
    try {
        // Apply constraints
        const stream = await navigator.mediaDevices.getUserMedia(mediaConstraints);
        localStreams[userId] = stream;

        // Set local video
        const localVideo = document.getElementById('local-video');
        if (localVideo) {
            localVideo.srcObject = stream;
        }

        const pc = new RTCPeerConnection({
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' }
            ]
        });
        peerConnections[userId] = pc;

        // Add tracks to peer connection
        stream.getTracks().forEach(track => {
            pc.addTrack(track, stream);
            console.log(`Added ${track.kind} track: ${track.label}`);
        });

        // Handle ICE candidates
        pc.onicecandidate = (event) => {
            if (event.candidate) {
                dotnetRef.invokeMethodAsync('HandleIceCandidate', JSON.stringify({
                    candidate: event.candidate.candidate,
                    sdpMid: event.candidate.sdpMid,
                    sdpMLineIndex: event.candidate.sdpMLineIndex
                }));
            }
        };

        // Handle remote streams
        pc.ontrack = (event) => {
            console.log('Remote stream received for user:', userId);
            const remoteVideo = document.getElementById(`remote-video-${userId}`);
            if (remoteVideo) {
                remoteVideo.srcObject = event.streams[0];
                // Notify Blazor
                dotnetRef.invokeMethodAsync('OnRemoteStreamAdded', userId, event.streams[0].id);
            }
        };

        // Create and send offer
        pc.onnegotiationneeded = async () => {
            try {
                const offer = await pc.createOffer();
                await pc.setLocalDescription(offer);
                dotnetRef.invokeMethodAsync('SendOfferToJanus', offer.sdp);
            } catch (error) {
                console.error('Error creating offer:', error);
            }
        };

        console.log(`User ${displayName} joined room ${roomId} with WebRTC`);
    } catch (error) {
        console.error('getUserMediaAndPublish error:', error);
        throw error; // Let Blazor handle the error
    }
};

window.handleRemoteDescription = async (type, sdp) => {
    try {
        const pc = peerConnections[currentUserId];
        if (pc) {
            const desc = new RTCSessionDescription({ type, sdp });
            await pc.setRemoteDescription(desc);

            if (type === 'offer') {
                const answer = await pc.createAnswer();
                await pc.setLocalDescription(answer);
                dotnetRef.invokeMethodAsync('SendAnswerToJanus', answer.sdp);
            }
        }
    } catch (error) {
        console.error('handleRemoteDescription error:', error);
    }
};

window.toggleMute = (userId, isMuted) => {
    const stream = localStreams[userId];
    if (stream) {
        const audioTracks = stream.getAudioTracks();
        audioTracks.forEach(track => {
            track.enabled = !isMuted;
            console.log(`Audio ${isMuted ? 'muted' : 'unmuted'} for ${userId}`);
        });
    }
};

window.startScreenShare = async (userId) => {
    try {
        const screenStream = await navigator.mediaDevices.getDisplayMedia({
            video: {
                width: { ideal: 1920 },
                height: { ideal: 1080 },
                frameRate: { ideal: 30 }
            }
        });

        const pc = peerConnections[userId];
        if (pc) {
            // Replace video track with screen share
            const sender = pc.getSenders().find(s => s.track && s.track.kind === 'video');
            if (sender) {
                sender.replaceTrack(screenStream.getVideoTracks()[0]);
                console.log('Screen share started');
            }

            // Stop previous camera stream
            const cameraStream = localStreams[userId];
            if (cameraStream) {
                cameraStream.getVideoTracks().forEach(track => track.stop());
            }

            // Update local video to show screen share
            const localVideo = document.getElementById('local-video');
            if (localVideo) {
                localVideo.srcObject = screenStream;
            }

            localStreams[userId] = screenStream;
        }
    } catch (error) {
        console.error('Screen share error:', error);
        throw error;
    }
};

window.cleanupPeerConnection = (userId) => {
    const pc = peerConnections[userId];
    if (pc) {
        pc.close();
        delete peerConnections[userId];
    }

    const stream = localStreams[userId];
    if (stream) {
        stream.getTracks().forEach(track => track.stop());
        delete localStreams[userId];
    }

    const videoElement = document.getElementById(`remote-video-${userId}`);
    if (videoElement) {
        videoElement.srcObject = null;
    }
};

window.cleanupAllPeerConnections = () => {
    Object.keys(peerConnections).forEach(userId => cleanupPeerConnection(userId));
};

window.initializeRemoteVideo = (userId) => {
    // Prepare video element for incoming stream
    const videoElement = document.getElementById(`remote-video-${userId}`);
    if (videoElement && !videoElement.srcObject) {
        videoElement.muted = false;
        videoElement.playsInline = true;
    }
};

window.cleanupRemoteVideo = (userId) => {
    const videoElement = document.getElementById(`remote-video-${userId}`);
    if (videoElement) {
        videoElement.srcObject = null;
        videoElement.pause();
    }
    cleanupPeerConnection(userId);
};

window.setLocalVideoElement = (elementId) => {
    const localVideo = document.getElementById(elementId);
    if (localVideo && localStreams[currentUserId]) {
        localVideo.srcObject = localStreams[currentUserId];
        localVideo.muted = true;
        localVideo.playsInline = true;
    }
};