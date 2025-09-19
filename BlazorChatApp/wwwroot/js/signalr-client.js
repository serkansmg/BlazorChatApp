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
window.scrollToBottom = (element) => {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};