// Janus WebRTC Video Call Handler
class JanusVideoCall {
    constructor() {
        this.localStream = null;
        this.remoteStream = null;
        this.peerConnection = null;
        this.janusSession = null;
        this.isActive = false;
        this.pendingIceCandidates = [];

        this.rtcConfiguration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' }
            ]
        };
    }

    async initialize(connectionParams) {
        try {
            console.log("Initializing Janus video call...", connectionParams);

            this.janusSession = {
                id: connectionParams.sessionId,
                handleId: connectionParams.handleId,
                roomId: connectionParams.roomId,
                janusUrl: connectionParams.janusUrl,
                displayName: connectionParams.displayName
            };

            // Get user media
            this.localStream = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: true
            });

            // Display local video
            const localVideo = document.getElementById('local-video');
            if (localVideo) {
                localVideo.srcObject = this.localStream;
            }

            // Create peer connection
            this.peerConnection = new RTCPeerConnection(this.rtcConfiguration);

            // Add local stream
            this.localStream.getTracks().forEach(track => {
                this.peerConnection.addTrack(track, this.localStream);
            });

            // Handle remote stream
            this.peerConnection.ontrack = (event) => {
                console.log("Remote stream received");
                this.remoteStream = event.streams[0];
                const remoteVideo = document.getElementById('remote-video');
                if (remoteVideo) {
                    remoteVideo.srcObject = this.remoteStream;
                }
            };

            // Handle ICE candidates
            this.peerConnection.onicecandidate = (event) => {
                if (event.candidate) {
                    console.log("ICE candidate generated");
                    // ICE candidate'ı SignalR ile gönder
                    this.sendSignal('ice_candidate', event.candidate);
                }
            };

            this.isActive = true;
            console.log("Janus video call initialized successfully");

        } catch (error) {
            console.error("Error initializing Janus video call:", error);
            throw error;
        }
    }
    sendSignal(type, data) {
        // SignalR üzerinden signal gönder
        if (window.sendVideoCallSignal && this.targetUserId) {
            window.sendVideoCallSignal(this.targetUserId, type, JSON.stringify(data));
        } else {
            console.error("Cannot send signal - targetUserId not set or sendVideoCallSignal not available");
            console.log("targetUserId:", this.targetUserId);
        }

        // .NET handler'a da bildir
        if (window.videoCallDotNetHelper && this.targetUserId) {
            window.videoCallDotNetHelper.invokeMethodAsync('OnVideoSignalGenerated', type, JSON.stringify(data), this.targetUserId);
        }
    }
    async createOffer() {
        try {
            if (!this.peerConnection) {
                throw new Error("Peer connection not initialized");
            }

            const offer = await this.peerConnection.createOffer();
            await this.peerConnection.setLocalDescription(offer);

            console.log("Offer created");
            this.sendSignal('offer', offer);

        } catch (error) {
            console.error("Error creating offer:", error);
        }
    }

    async handleAnswer(answer) {
        try {
            await this.peerConnection.setRemoteDescription(new RTCSessionDescription(answer));

            // Pending ICE candidate'ları ekle
            if (this.pendingIceCandidates) {
                console.log(`Adding ${this.pendingIceCandidates.length} pending ICE candidates`);
                for (const candidate of this.pendingIceCandidates) {
                    try {
                        await this.peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
                    } catch (err) {
                        console.error("Error adding pending ICE candidate:", err);
                    }
                }
                this.pendingIceCandidates = [];
            }

            console.log("Answer handled");
        } catch (error) {
            console.error("Error handling answer:", error);
        }
    }

    async handleOffer(offer) {
        try {
            await this.peerConnection.setRemoteDescription(new RTCSessionDescription(offer));

            // Pending ICE candidate'ları ekle
            if (this.pendingIceCandidates) {
                console.log(`Adding ${this.pendingIceCandidates.length} pending ICE candidates`);
                for (const candidate of this.pendingIceCandidates) {
                    try {
                        await this.peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
                    } catch (err) {
                        console.error("Error adding pending ICE candidate:", err);
                    }
                }
                this.pendingIceCandidates = [];
            }

            const answer = await this.peerConnection.createAnswer();
            await this.peerConnection.setLocalDescription(answer);

            console.log("Answer created for offer");
            this.sendSignal('answer', answer);

        } catch (error) {
            console.error("Error handling offer:", error);
        }
    }

    async handleIceCandidate(candidate) {
        try {
            // Remote description henüz set edilmemişse ICE candidate'ları queue'da beklet
            if (!this.peerConnection.remoteDescription) {
                console.log("Remote description not set yet, queuing ICE candidate");
                if (!this.pendingIceCandidates) {
                    this.pendingIceCandidates = [];
                }
                this.pendingIceCandidates.push(candidate);
                return;
            }

            await this.peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
            console.log("ICE candidate added");
        } catch (error) {
            console.error("Error adding ICE candidate:", error);
        }
    }
 

    setTargetUserId(userId) {
        this.targetUserId = userId;
    }

    toggleMute(isMuted) {
        if (this.localStream) {
            const audioTrack = this.localStream.getAudioTracks()[0];
            if (audioTrack) {
                audioTrack.enabled = !isMuted;
                console.log("Audio muted:", isMuted);
            }
        }
    }

    toggleVideo(isVideoEnabled) {
        if (this.localStream) {
            const videoTrack = this.localStream.getVideoTracks()[0];
            if (videoTrack) {
                videoTrack.enabled = isVideoEnabled;
                console.log("Video enabled:", isVideoEnabled);
            }
        }
    }

    async toggleScreenShare(isScreenSharing) {
        try {
            if (isScreenSharing) {
                const screenStream = await navigator.mediaDevices.getDisplayMedia({
                    video: true,
                    audio: true
                });

                if (this.peerConnection) {
                    const videoTrack = screenStream.getVideoTracks()[0];
                    const sender = this.peerConnection.getSenders().find(s =>
                        s.track && s.track.kind === 'video'
                    );
                    if (sender) {
                        await sender.replaceTrack(videoTrack);
                    }
                }

                console.log("Screen sharing started");
            } else {
                const cameraStream = await navigator.mediaDevices.getUserMedia({
                    video: true,
                    audio: true
                });

                if (this.peerConnection) {
                    const videoTrack = cameraStream.getVideoTracks()[0];
                    const sender = this.peerConnection.getSenders().find(s =>
                        s.track && s.track.kind === 'video'
                    );
                    if (sender) {
                        await sender.replaceTrack(videoTrack);
                    }
                }

                console.log("Screen sharing stopped");
            }
        } catch (error) {
            console.error("Error toggling screen share:", error);
        }
    }

    async cleanup() {
        try {
            console.log("Cleaning up Janus video call...");

            if (this.localStream) {
                this.localStream.getTracks().forEach(track => track.stop());
                this.localStream = null;
            }

            if (this.peerConnection) {
                this.peerConnection.close();
                this.peerConnection = null;
            }

            const localVideo = document.getElementById('local-video');
            const remoteVideo = document.getElementById('remote-video');

            if (localVideo) localVideo.srcObject = null;
            if (remoteVideo) remoteVideo.srcObject = null;

            this.isActive = false;
            this.janusSession = null;

            console.log("Janus video call cleaned up");

        } catch (error) {
            console.error("Error cleaning up video call:", error);
        }
    }
}

// Global instance
window.janusVideoCall = new JanusVideoCall();

// Global functions for Blazor
window.initializeJanusVideoCall = async (connectionParams) => {
    await window.janusVideoCall.initialize(connectionParams);
};

window.startJanusVideoCall = async () => {
    await window.janusVideoCall.createOffer();
};

window.endJanusVideoCall = async () => {
    await window.janusVideoCall.cleanup();
};

window.setVideoCallTarget = (userId) => {
    window.janusVideoCall.setTargetUserId(userId);
};

window.handleVideoSignal = async (signalType, data) => {
    const signalData = JSON.parse(data);

    switch (signalType) {
        case 'offer':
            await window.janusVideoCall.handleOffer(signalData);
            break;
        case 'answer':
            await window.janusVideoCall.handleAnswer(signalData);
            break;
        case 'ice_candidate':
            await window.janusVideoCall.handleIceCandidate(signalData);
            break;
    }
};

window.setVideoCallHandler = (dotNetHelper) => {
    console.log("Video call handler set:", dotNetHelper);
    window.videoCallDotNetHelper = dotNetHelper;
};

window.toggleMute = (isMuted) => {
    window.janusVideoCall.toggleMute(isMuted);
};

window.toggleVideo = (isVideoEnabled) => {
    window.janusVideoCall.toggleVideo(isVideoEnabled);
};

window.toggleScreenShare = async (isScreenSharing) => {
    await window.janusVideoCall.toggleScreenShare(isScreenSharing);
};
window.handleCallAccepted = () => {
    console.log("Call accepted - notifying .NET");
    if (window.videoCallDotNetHelper) {
        window.videoCallDotNetHelper.invokeMethodAsync('OnCallAccepted');
    }
};

// Media device management
let availableVideoDevices = [];
let availableAudioDevices = [];

window.loadMediaDevices = async () => {
    try {
        // Permission iste
        await navigator.mediaDevices.getUserMedia({ video: true, audio: true });

        const devices = await navigator.mediaDevices.enumerateDevices();

        availableVideoDevices = devices
            .filter(device => device.kind === 'videoinput')
            .map(device => ({
                deviceId: device.deviceId,
                label: device.label || `Camera ${device.deviceId.substring(0, 8)}...`
            }));

        availableAudioDevices = devices
            .filter(device => device.kind === 'audioinput')
            .map(device => ({
                deviceId: device.deviceId,
                label: device.label || `Microphone ${device.deviceId.substring(0, 8)}...`
            }));

        // Add screen share option
        availableVideoDevices.push({ deviceId: 'screen', label: 'Ekran Paylaşımı' });

        console.log('Available video devices:', availableVideoDevices);
        console.log('Available audio devices:', availableAudioDevices);

        // JSON string olarak gönder
        if (window.videoCallDotNetHelper) {
            window.videoCallDotNetHelper.invokeMethodAsync('OnMediaDevicesLoaded',
                JSON.stringify(availableVideoDevices),
                JSON.stringify(availableAudioDevices));
        }

    } catch (error) {
        console.error('Error loading media devices:', error);
    }
};

window.switchVideoSource = async (deviceId) => {
    try {
        let videoConstraints;

        if (deviceId === 'screen') {
            // Screen sharing
            const screenStream = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: true
            });

            await replaceVideoTrack(screenStream.getVideoTracks()[0]);
        } else {
            // Camera
            const cameraStream = await navigator.mediaDevices.getUserMedia({
                video: { deviceId: deviceId ? { exact: deviceId } : undefined },
                audio: false
            });

            await replaceVideoTrack(cameraStream.getVideoTracks()[0]);
        }

    } catch (error) {
        console.error('Error switching video source:', error);
    }
};

window.switchAudioSource = async (deviceId) => {
    try {
        const audioStream = await navigator.mediaDevices.getUserMedia({
            audio: { deviceId: deviceId ? { exact: deviceId } : undefined },
            video: false
        });

        await replaceAudioTrack(audioStream.getAudioTracks()[0]);

    } catch (error) {
        console.error('Error switching audio source:', error);
    }
};

async function replaceVideoTrack(newTrack) {
    if (window.janusVideoCall.peerConnection) {
        const sender = window.janusVideoCall.peerConnection.getSenders()
            .find(s => s.track && s.track.kind === 'video');

        if (sender) {
            await sender.replaceTrack(newTrack);
        }
    }

    // Update local video display
    const localVideo = document.getElementById('local-video');
    if (localVideo && newTrack) {
        const stream = new MediaStream([newTrack]);
        localVideo.srcObject = stream;
    }
}

async function replaceAudioTrack(newTrack) {
    if (window.janusVideoCall.peerConnection) {
        const sender = window.janusVideoCall.peerConnection.getSenders()
            .find(s => s.track && s.track.kind === 'audio');

        if (sender) {
            await sender.replaceTrack(newTrack);
        }
    }
}

window.startScreenCapture = async () => {
    await window.switchVideoSource('screen');
};