class ChatSignalR {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.dotNetHelper = null;
    }

    // C# tarafından çağrılacak
    setDotNetHelper(dotNetHelper) {
        this.dotNetHelper = dotNetHelper;
        console.log("DotNet helper set:", this.dotNetHelper);
    }

    async start() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .withAutomaticReconnect()
            .build();

        // Event handlers - C# tarafını çağır
        this.connection.on("ReceiveMessage", (message) => {
            console.log("ReceiveMessage event received:", message);
            if (this.dotNetHelper) {
                console.log("Calling OnMessageReceived...");
                this.dotNetHelper.invokeMethodAsync('OnMessageReceived', message)
                    .then(() => console.log("OnMessageReceived called successfully"))
                    .catch(err => console.error("OnMessageReceived failed:", err));
            } else {
                console.error("dotNetHelper is null!");
            }
        });

        this.connection.on("ReceiveGroupMessage", (message) => {
            console.log("ReceiveGroupMessage event received:", message);
            if (this.dotNetHelper) {
                console.log("Calling OnGroupMessageReceived...");
                this.dotNetHelper.invokeMethodAsync('OnGroupMessageReceived', message)
                    .then(() => console.log("OnGroupMessageReceived called successfully"))
                    .catch(err => console.error("OnGroupMessageReceived failed:", err));
            } else {
                console.error("dotNetHelper is null!");
            }
        });

        this.connection.on("UserOnline", (userId) => {
            console.log("UserOnline event received:", userId);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnUserOnline', userId);
            }
        });

        this.connection.on("UserOffline", (userId) => {
            console.log("UserOffline event received:", userId);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnUserOffline', userId);
            }
        });

        this.connection.on("UserTyping", (userId) => {
            console.log("UserTyping event received:", userId);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnUserTyping', userId);
            }
        });

        // Friend request event handlers - CLASS İÇİNDE
        this.connection.on("FriendRequestReceived", (data) => {
            console.log("FriendRequestReceived event received:", data);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnFriendRequestReceived', data);
            }
        });

        this.connection.on("FriendRequestAccepted", (data) => {
            console.log("FriendRequestAccepted event received:", data);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnFriendRequestAccepted', data);
            }
        });

        this.connection.on("FriendRequestRejected", (data) => {
            console.log("FriendRequestRejected event received:", data);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnFriendRequestRejected', data);
            }
        });

        this.connection.on("ReceiveVideoCallSignal", (signalData) => {
            console.log("ReceiveVideoCallSignal event received:", signalData);
            console.log("SignalData keys:", Object.keys(signalData));
            console.log("DotNetHelper exists:", !!this.dotNetHelper);

            if (this.dotNetHelper) {
                console.log("Calling OnVideoCallSignalReceived...");
                this.dotNetHelper.invokeMethodAsync('OnVideoCallSignalReceived', signalData)
                    .then(() => console.log("OnVideoCallSignalReceived completed"))
                    .catch(err => console.error("OnVideoCallSignalReceived failed:", err));
            }
        });

        try {
            await this.connection.start();
            this.isConnected = true;
            console.log("SignalR connected successfully");

            // Bağlantı durumunu C# tarafına bildir
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnConnectionStatusChanged', true);
            }
        } catch (err) {
            console.error("SignalR connection failed:", err);
            if (this.dotNetHelper) {
                this.dotNetHelper.invokeMethodAsync('OnConnectionStatusChanged', false);
            }
        }
    }


    async sendMediaMessageToUser(receiverId, content, messageType, fileUrl, fileName, fileSize, mimeType) {
        console.log("Sending media message to user:", receiverId, messageType);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendMediaMessageToUser", receiverId, content, messageType, fileUrl, fileName, fileSize, mimeType);
                console.log("Media message sent successfully");
            } catch (err) {
                console.error("Failed to send media message:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async sendMediaMessageToGroup(groupId, content, messageType, fileUrl, fileName, fileSize, mimeType) {
        console.log("Sending media message to group:", groupId, messageType);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendMediaMessageToGroup", groupId, content, messageType, fileUrl, fileName, fileSize, mimeType);
                console.log("Group media message sent successfully");
            } catch (err) {
                console.error("Failed to send group media message:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async sendVideoCallSignal(receiverId, signalType, data) {
        console.log("Sending video call signal:", receiverId, signalType);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendVideoCallSignal", receiverId, signalType, data);
                console.log("Video call signal sent successfully");
            } catch (err) {
                console.error("Failed to send video call signal:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async sendMessageToUser(receiverId, message) {
        console.log("Sending message to user:", receiverId, message);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendMessageToUser", receiverId, message);
                console.log("Message sent successfully");
            } catch (err) {
                console.error("Failed to send message:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async sendMessageToGroup(groupId, message) {
        console.log("Sending message to group:", groupId, message);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendMessageToGroup", groupId, message);
                console.log("Group message sent successfully");
            } catch (err) {
                console.error("Failed to send group message:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async joinGroup(groupId) {
        console.log("Joining group:", groupId);
        if (this.isConnected) {
            await this.connection.invoke("JoinGroup", groupId);
        }
    }

    async sendTypingNotification(receiverId) {
        if (this.isConnected) {
            await this.connection.invoke("SendTypingNotification", receiverId);
        }
    }

    // Friend request metodları - CLASS İÇİNDE
    async sendFriendRequest(receiverId, senderName, message) {
        console.log("Sending friend request:", receiverId, senderName, message);
        if (this.isConnected) {
            try {
                await this.connection.invoke("SendFriendRequest", receiverId, senderName, message);
                console.log("Friend request sent successfully");
            } catch (err) {
                console.error("Failed to send friend request:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }

    async acceptFriendRequest(senderId, accepterName) {
        console.log("Accepting friend request:", senderId, accepterName);
        if (this.isConnected) {
            try {
                await this.connection.invoke("AcceptFriendRequest", senderId, accepterName);
                console.log("Friend request accepted successfully");
            } catch (err) {
                console.error("Failed to accept friend request:", err);
            }
        } else {
            console.error("SignalR not connected!");
        }
    }
}

// Global instance
window.chatSignalR = new ChatSignalR();

// Global functions for C# interop
window.initializeSignalR = async (dotNetHelper) => {
    console.log("Initializing SignalR with dotNetHelper:", dotNetHelper);
    window.chatSignalR.setDotNetHelper(dotNetHelper);
    await window.chatSignalR.start();
};

window.sendMessageToUser = async (receiverId, message) => {
    await window.chatSignalR.sendMessageToUser(receiverId, message);
};

window.sendMessageToGroup = async (groupId, message) => {
    await window.chatSignalR.sendMessageToGroup(groupId, message);
};

window.joinGroup = async (groupId) => {
    await window.chatSignalR.joinGroup(groupId);
};

window.sendTypingNotification = async (receiverId) => {
    await window.chatSignalR.sendTypingNotification(receiverId);
};

// Friend request global functions - INSTANCE METODLARINI ÇAĞIR
window.sendFriendRequest = async (receiverId, senderName, message) => {
    await window.chatSignalR.sendFriendRequest(receiverId, senderName, message);
};

window.acceptFriendRequest = async (senderId, accepterName) => {
    await window.chatSignalR.acceptFriendRequest(senderId, accepterName);
};
window.sendMediaMessageToUser = async (receiverId, content, messageType, fileUrl, fileName, fileSize, mimeType) => {
    await window.chatSignalR.sendMediaMessageToUser(receiverId, content, messageType, fileUrl, fileName, fileSize, mimeType);
};

window.sendMediaMessageToGroup = async (groupId, content, messageType, fileUrl, fileName, fileSize, mimeType) => {
    await window.chatSignalR.sendMediaMessageToGroup(groupId, content, messageType, fileUrl, fileName, fileSize, mimeType);
};

window.sendVideoCallSignal = async (receiverId, signalType, data) => {
    await window.chatSignalR.sendVideoCallSignal(receiverId, signalType, data);
};

window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

// WebRTC için global değişkenler
let localStream = null;
let remoteStream = null;
let peerConnection = null;
let isVideoCallActive = false;

// WebRTC configuration
const rtcConfiguration = {
    iceServers: [
        { urls: 'stun:stun.l.google.com:19302' },
        { urls: 'stun:stun1.l.google.com:19302' }
    ]
};

// Video call functions - dosyanın sonuna ekle:
window.initializeVideoCall = async (connectionParams) => {
    try {
        console.log("Initializing video call...");

        // Get user media
        localStream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: true
        });

        // Display local video
        const localVideo = document.getElementById('local-video');
        if (localVideo) {
            localVideo.srcObject = localStream;
        }

        // Create peer connection
        peerConnection = new RTCPeerConnection(rtcConfiguration);

        // Add local stream to peer connection
        localStream.getTracks().forEach(track => {
            peerConnection.addTrack(track, localStream);
        });

        // Handle remote stream
        peerConnection.ontrack = (event) => {
            console.log("Remote stream received");
            remoteStream = event.streams[0];
            const remoteVideo = document.getElementById('remote-video');
            if (remoteVideo) {
                remoteVideo.srcObject = remoteStream;
            }
        };

        // Handle ICE candidates
        peerConnection.onicecandidate = (event) => {
            if (event.candidate) {
                console.log("ICE candidate generated");
                // Bu ICE candidate'i SignalR ile karşı tarafa gönder
                // window.sendVideoCallSignal(receiverId, "ice_candidate", event.candidate);
            }
        };

        isVideoCallActive = true;
        console.log("Video call initialized successfully");

    } catch (error) {
        console.error("Error initializing video call:", error);
        throw error;
    }
};

window.startVideoCall = async () => {
    try {
        if (!peerConnection) {
            throw new Error("Peer connection not initialized");
        }

        // Create offer
        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);

        console.log("Video call started with offer");
        // Offer'ı SignalR ile karşı tarafa gönder
        // window.sendVideoCallSignal(receiverId, "offer", offer);

    } catch (error) {
        console.error("Error starting video call:", error);
    }
};

window.endVideoCall = async () => {
    try {
        console.log("Ending video call...");

        // Stop local stream
        if (localStream) {
            localStream.getTracks().forEach(track => track.stop());
            localStream = null;
        }

        // Close peer connection
        if (peerConnection) {
            peerConnection.close();
            peerConnection = null;
        }

        // Clear video elements
        const localVideo = document.getElementById('local-video');
        const remoteVideo = document.getElementById('remote-video');

        if (localVideo) localVideo.srcObject = null;
        if (remoteVideo) remoteVideo.srcObject = null;

        isVideoCallActive = false;
        console.log("Video call ended");

    } catch (error) {
        console.error("Error ending video call:", error);
    }
};

window.toggleMute = (isMuted) => {
    if (localStream) {
        const audioTrack = localStream.getAudioTracks()[0];
        if (audioTrack) {
            audioTrack.enabled = !isMuted;
            console.log("Audio muted:", isMuted);
        }
    }
};

window.toggleVideo = (isVideoEnabled) => {
    if (localStream) {
        const videoTrack = localStream.getVideoTracks()[0];
        if (videoTrack) {
            videoTrack.enabled = isVideoEnabled;
            console.log("Video enabled:", isVideoEnabled);
        }
    }
};

window.toggleScreenShare = async (isScreenSharing) => {
    try {
        if (isScreenSharing) {
            // Start screen sharing
            const screenStream = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: true
            });

            // Replace video track in peer connection
            if (peerConnection) {
                const videoTrack = screenStream.getVideoTracks()[0];
                const sender = peerConnection.getSenders().find(s =>
                    s.track && s.track.kind === 'video'
                );
                if (sender) {
                    await sender.replaceTrack(videoTrack);
                }
            }

            console.log("Screen sharing started");
        } else {
            // Stop screen sharing, return to camera
            const cameraStream = await navigator.mediaDevices.getUserMedia({
                video: true,
                audio: true
            });

            if (peerConnection) {
                const videoTrack = cameraStream.getVideoTracks()[0];
                const sender = peerConnection.getSenders().find(s =>
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
};