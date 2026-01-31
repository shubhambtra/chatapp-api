# Chat Application - Backend API Documentation

This document outlines all the APIs required for a production-ready chat support application backend.

---

## Base URL

```
Production: https://api.yourdomain.com/v1
Development: http://localhost:8000/api/v1
```

---

## Authentication

All authenticated endpoints require the `Authorization` header:
```
Authorization: Bearer <token>
```

---

## 1. Authentication APIs

### 1.1 Support Agent Login

**POST** `/auth/support/login`

Authenticates a support agent and returns an access token.

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| username | string | Yes | Support agent username |
| password | string | Yes | Support agent password |
| site_id | string | Yes | Site ID to access |

**Request Example:**
```json
{
  "username": "alice",
  "password": "pass123",
  "site_id": "site_abc123"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refresh_token": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
    "expires_in": 3600,
    "user": {
      "id": "user_123",
      "username": "alice",
      "email": "alice@company.com",
      "role": "support_agent",
      "site_ids": ["site_abc123", "site_def456"]
    }
  }
}
```

**Error Response (401 Unauthorized):**
```json
{
  "success": false,
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "Invalid username or password"
  }
}
```

---

### 1.2 Refresh Token

**POST** `/auth/refresh`

Refreshes an expired access token.

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| refresh_token | string | Yes | The refresh token |

**Request Example:**
```json
{
  "refresh_token": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expires_in": 3600
  }
}
```

---

### 1.3 Logout

**POST** `/auth/logout`

Invalidates the current session token.

**Headers:** `Authorization: Bearer <token>`

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

### 1.4 Validate Token

**GET** `/auth/validate`

Validates if the current token is valid.

**Headers:** `Authorization: Bearer <token>`

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "valid": true,
    "user": {
      "id": "user_123",
      "username": "alice",
      "role": "support_agent"
    }
  }
}
```

---

## 2. Site Management APIs

### 2.1 Get Site Details

**GET** `/sites/{site_id}`

Retrieves site configuration and details.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | The site identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "site_abc123",
    "name": "My E-commerce Store",
    "domain": "https://mystore.com",
    "widget_config": {
      "primary_color": "#2563eb",
      "welcome_message": "Hi! How can we help you?",
      "offline_message": "We're currently offline. Leave a message!"
    },
    "created_at": "2024-01-15T10:30:00Z",
    "status": "active"
  }
}
```

---

### 2.2 List Sites for Agent

**GET** `/sites`

Lists all sites the authenticated support agent has access to.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| page | integer | No | Page number (default: 1) |
| limit | integer | No | Items per page (default: 20) |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "sites": [
      {
        "id": "site_abc123",
        "name": "My E-commerce Store",
        "domain": "https://mystore.com",
        "active_conversations": 5
      }
    ],
    "pagination": {
      "page": 1,
      "limit": 20,
      "total": 1
    }
  }
}
```

---

## 3. Visitor/Customer APIs

### 3.1 Register Visitor

**POST** `/visitors`

Registers a new visitor (called when customer starts chat).

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| visitor_id | string | Yes | Unique visitor ID (generated client-side) |
| name | string | Yes | Visitor's name |
| email | string | No | Visitor's email |
| metadata | object | No | Additional visitor info |

**Request Example:**
```json
{
  "site_id": "site_abc123",
  "visitor_id": "v_uuid_12345",
  "name": "John Doe",
  "email": "john@example.com",
  "metadata": {
    "page_url": "https://mystore.com/products",
    "user_agent": "Mozilla/5.0...",
    "referrer": "https://google.com"
  }
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "visitor_789",
    "visitor_id": "v_uuid_12345",
    "name": "John Doe",
    "session_token": "visitor_session_token_xyz",
    "created_at": "2024-01-20T14:30:00Z"
  }
}
```

---

### 3.2 Get Visitor Details

**GET** `/visitors/{visitor_id}`

Retrieves visitor details and history.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| visitor_id | string | Yes | The visitor identifier |

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "visitor_789",
    "visitor_id": "v_uuid_12345",
    "name": "John Doe",
    "email": "john@example.com",
    "metadata": {
      "page_url": "https://mystore.com/products",
      "user_agent": "Mozilla/5.0..."
    },
    "first_seen": "2024-01-15T10:00:00Z",
    "last_seen": "2024-01-20T14:30:00Z",
    "total_conversations": 3,
    "status": "online"
  }
}
```

---

### 3.3 List Active Visitors

**GET** `/visitors/active`

Lists all currently active visitors for a site.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "visitors": [
      {
        "visitor_id": "v_uuid_12345",
        "name": "John Doe",
        "status": "online",
        "current_page": "https://mystore.com/checkout",
        "unread_count": 2,
        "last_message_at": "2024-01-20T14:28:00Z"
      }
    ],
    "total": 1
  }
}
```

---

## 4. Conversation APIs

### 4.1 Create Conversation

**POST** `/conversations`

Creates a new conversation thread.

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| visitor_id | string | Yes | Visitor identifier |

**Request Example:**
```json
{
  "site_id": "site_abc123",
  "visitor_id": "v_uuid_12345"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "conv_456",
    "site_id": "site_abc123",
    "visitor_id": "v_uuid_12345",
    "status": "active",
    "created_at": "2024-01-20T14:30:00Z"
  }
}
```

---

### 4.2 Get Conversation

**GET** `/conversations/{conversation_id}`

Retrieves a conversation with its messages.

**Headers:** `Authorization: Bearer <token>` (for support) OR `X-Visitor-Token: <token>` (for visitor)

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| include_messages | boolean | No | Include messages (default: true) |
| messages_limit | integer | No | Number of messages (default: 50) |
| messages_before | string | No | Get messages before this message ID |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "conv_456",
    "site_id": "site_abc123",
    "visitor": {
      "id": "v_uuid_12345",
      "name": "John Doe"
    },
    "assigned_agent": {
      "id": "user_123",
      "username": "alice"
    },
    "status": "active",
    "created_at": "2024-01-20T14:30:00Z",
    "messages": [
      {
        "id": "msg_001",
        "from": "visitor",
        "content": "Hello, I need help",
        "type": "text",
        "created_at": "2024-01-20T14:30:00Z"
      }
    ],
    "has_more_messages": false
  }
}
```

---

### 4.3 List Conversations

**GET** `/conversations`

Lists conversations for a site.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| status | string | No | Filter by status (active, closed, all) |
| assigned_to | string | No | Filter by agent ID |
| page | integer | No | Page number (default: 1) |
| limit | integer | No | Items per page (default: 20) |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "conversations": [
      {
        "id": "conv_456",
        "visitor": {
          "id": "v_uuid_12345",
          "name": "John Doe"
        },
        "last_message": {
          "content": "Thanks for your help!",
          "from": "visitor",
          "created_at": "2024-01-20T14:45:00Z"
        },
        "unread_count": 1,
        "status": "active",
        "created_at": "2024-01-20T14:30:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "limit": 20,
      "total": 1
    }
  }
}
```

---

### 4.4 Close Conversation

**PATCH** `/conversations/{conversation_id}/close`

Closes/ends a conversation.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| resolution | string | No | Resolution notes |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "conv_456",
    "status": "closed",
    "closed_at": "2024-01-20T15:00:00Z",
    "closed_by": "user_123"
  }
}
```

---

### 4.5 Assign Conversation

**PATCH** `/conversations/{conversation_id}/assign`

Assigns a conversation to a support agent.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| agent_id | string | Yes | Agent to assign to |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "conv_456",
    "assigned_agent": {
      "id": "user_123",
      "username": "alice"
    },
    "assigned_at": "2024-01-20T14:35:00Z"
  }
}
```

---

## 5. Message APIs

### 5.1 Send Message

**POST** `/messages`

Sends a new message in a conversation.

**Headers:** `Authorization: Bearer <token>` (for support) OR `X-Visitor-Token: <token>` (for visitor)

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |
| content | string | Yes* | Message text content |
| type | string | Yes | Message type: `text`, `file`, `image` |
| file_id | string | Yes* | File ID (required if type is file/image) |
| metadata | object | No | Additional message metadata |

*Either `content` or `file_id` is required based on type.

**Request Example (Text):**
```json
{
  "conversation_id": "conv_456",
  "content": "Hello, how can I help you today?",
  "type": "text"
}
```

**Request Example (File):**
```json
{
  "conversation_id": "conv_456",
  "content": "Here's the document you requested",
  "type": "file",
  "file_id": "file_abc123"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "msg_002",
    "conversation_id": "conv_456",
    "from": "support",
    "sender": {
      "id": "user_123",
      "name": "Alice"
    },
    "content": "Hello, how can I help you today?",
    "type": "text",
    "created_at": "2024-01-20T14:32:00Z",
    "status": "sent"
  }
}
```

---

### 5.2 Get Messages

**GET** `/messages`

Retrieves messages for a conversation.

**Headers:** `Authorization: Bearer <token>` OR `X-Visitor-Token: <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |
| limit | integer | No | Number of messages (default: 50) |
| before | string | No | Get messages before this message ID |
| after | string | No | Get messages after this message ID |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "messages": [
      {
        "id": "msg_001",
        "conversation_id": "conv_456",
        "from": "visitor",
        "sender": {
          "id": "v_uuid_12345",
          "name": "John Doe"
        },
        "content": "Hello, I need help with my order",
        "type": "text",
        "created_at": "2024-01-20T14:30:00Z",
        "status": "delivered"
      },
      {
        "id": "msg_002",
        "conversation_id": "conv_456",
        "from": "support",
        "sender": {
          "id": "user_123",
          "name": "Alice"
        },
        "content": "Hi John! I'd be happy to help.",
        "type": "text",
        "created_at": "2024-01-20T14:32:00Z",
        "status": "read"
      }
    ],
    "has_more": false
  }
}
```

---

### 5.3 Mark Messages as Read

**POST** `/messages/read`

Marks messages as read.

**Headers:** `Authorization: Bearer <token>` OR `X-Visitor-Token: <token>`

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |
| message_ids | array | No | Specific message IDs (if empty, marks all as read) |

**Request Example:**
```json
{
  "conversation_id": "conv_456",
  "message_ids": ["msg_001", "msg_002"]
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "marked_count": 2
  }
}
```

---

## 6. File Management APIs

### 6.1 Upload File

**POST** `/files/upload`

Uploads a file (image or document).

**Headers:** `Authorization: Bearer <token>` OR `X-Visitor-Token: <token>`

**Content-Type:** `multipart/form-data`

**Form Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| file | file | Yes | The file to upload |
| site_id | string | Yes | Site identifier |
| conversation_id | string | No | Associated conversation |

**Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": "file_abc123",
    "filename": "a1b2c3d4e5f6.jpg",
    "original_name": "product-screenshot.jpg",
    "mime_type": "image/jpeg",
    "size": 245678,
    "url": "/files/a1b2c3d4e5f6.jpg",
    "thumbnail_url": "/files/thumb_a1b2c3d4e5f6.jpg",
    "is_image": true,
    "created_at": "2024-01-20T14:33:00Z"
  }
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "error": {
    "code": "FILE_TOO_LARGE",
    "message": "File size exceeds maximum limit of 10MB"
  }
}
```

---

### 6.2 Get File

**GET** `/files/{file_id}`

Retrieves file metadata.

**Headers:** `Authorization: Bearer <token>` OR `X-Visitor-Token: <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| file_id | string | Yes | File identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "file_abc123",
    "filename": "a1b2c3d4e5f6.jpg",
    "original_name": "product-screenshot.jpg",
    "mime_type": "image/jpeg",
    "size": 245678,
    "url": "/files/a1b2c3d4e5f6.jpg",
    "is_image": true,
    "uploaded_by": {
      "type": "visitor",
      "id": "v_uuid_12345"
    },
    "created_at": "2024-01-20T14:33:00Z"
  }
}
```

---

### 6.3 Download File

**GET** `/files/{file_id}/download`

Downloads the actual file.

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| file_id | string | Yes | File identifier |

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| token | string | Yes | Access token (for direct download links) |

**Response:** File binary with appropriate Content-Type header.

---

### 6.4 Delete File

**DELETE** `/files/{file_id}`

Deletes an uploaded file.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| file_id | string | Yes | File identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "message": "File deleted successfully"
}
```

---

## 7. AI Analysis APIs

### 7.1 Analyze Message

**POST** `/analysis/message`

Analyzes a customer message for insights.

**Headers:** `Authorization: Bearer <token>`

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| message | string | Yes | The message to analyze |
| conversation_id | string | No | Conversation context |
| visitor_id | string | No | Visitor context |

**Request Example:**
```json
{
  "message": "I'm interested in your premium plan but the price seems a bit high",
  "conversation_id": "conv_456"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "suggested_reply": "I understand price is an important factor. Our premium plan offers [key benefits]. Would you like me to explain the value you'll get, or would a custom plan better suit your needs?",
    "interest_level": "High",
    "conversion_percentage": 65,
    "objection": "Price concern",
    "next_action": "Address pricing objection with value proposition",
    "sentiment": "neutral",
    "intent": "purchase_inquiry",
    "keywords": ["premium plan", "price", "interested"]
  }
}
```

---

### 7.2 Get Conversation Analysis

**GET** `/analysis/conversation/{conversation_id}`

Gets aggregated analysis for an entire conversation.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "conversation_id": "conv_456",
    "overall_sentiment": "positive",
    "interest_level": "High",
    "conversion_probability": 75,
    "key_topics": ["pricing", "features", "support"],
    "objections_raised": ["price concern"],
    "recommended_actions": [
      "Offer trial period",
      "Highlight ROI benefits"
    ],
    "summary": "Customer is interested in premium plan but has price concerns. Responded positively to feature explanations."
  }
}
```

---

## 8. Typing Indicator APIs

### 8.1 Send Typing Status

**POST** `/typing`

Broadcasts typing status.

**Headers:** `Authorization: Bearer <token>` OR `X-Visitor-Token: <token>`

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| conversation_id | string | Yes | Conversation identifier |
| is_typing | boolean | Yes | Whether user is typing |

**Request Example:**
```json
{
  "conversation_id": "conv_456",
  "is_typing": true
}
```

**Response (200 OK):**
```json
{
  "success": true
}
```

---

## 9. Notification APIs

### 9.1 Get Notifications

**GET** `/notifications`

Gets notifications for the authenticated user.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| unread_only | boolean | No | Only unread notifications |
| limit | integer | No | Number of notifications (default: 20) |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "notifications": [
      {
        "id": "notif_001",
        "type": "new_conversation",
        "title": "New conversation",
        "message": "John Doe started a new conversation",
        "data": {
          "conversation_id": "conv_456",
          "visitor_name": "John Doe"
        },
        "read": false,
        "created_at": "2024-01-20T14:30:00Z"
      }
    ],
    "unread_count": 5
  }
}
```

---

### 9.2 Mark Notifications Read

**POST** `/notifications/read`

Marks notifications as read.

**Headers:** `Authorization: Bearer <token>`

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| notification_ids | array | No | Specific IDs (empty = mark all) |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "marked_count": 5
  }
}
```

---

## 10. Support Agent Management APIs

### 10.1 List Agents

**GET** `/agents`

Lists support agents for a site.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| status | string | No | Filter: online, offline, all |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "agents": [
      {
        "id": "user_123",
        "username": "alice",
        "email": "alice@company.com",
        "status": "online",
        "active_conversations": 3,
        "last_seen": "2024-01-20T14:45:00Z"
      }
    ]
  }
}
```

---

### 10.2 Update Agent Status

**PATCH** `/agents/status`

Updates the agent's online status.

**Headers:** `Authorization: Bearer <token>`

**Request Body:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| status | string | Yes | Status: online, away, offline |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "status": "online",
    "updated_at": "2024-01-20T14:45:00Z"
  }
}
```

---

## 11. Analytics APIs

### 11.1 Get Dashboard Stats

**GET** `/analytics/dashboard`

Gets dashboard statistics.

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| period | string | No | today, week, month (default: today) |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "total_conversations": 45,
    "active_conversations": 8,
    "messages_sent": 234,
    "average_response_time": 45,
    "customer_satisfaction": 4.5,
    "conversations_by_hour": [
      {"hour": 9, "count": 5},
      {"hour": 10, "count": 8}
    ]
  }
}
```

---

### 11.2 Get Agent Performance

**GET** `/analytics/agents/{agent_id}`

Gets performance metrics for an agent.

**Headers:** `Authorization: Bearer <token>`

**Path Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| agent_id | string | Yes | Agent identifier |

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| period | string | No | today, week, month |

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "agent_id": "user_123",
    "conversations_handled": 25,
    "messages_sent": 156,
    "average_response_time": 32,
    "average_resolution_time": 480,
    "customer_satisfaction": 4.7
  }
}
```

---

## 12. WebSocket Events

### Connection URL

```
ws://api.yourdomain.com/ws?site_id={site_id}&role={role}&token={token}
```

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| site_id | string | Yes | Site identifier |
| role | string | Yes | `support` or `visitor` |
| token | string | Yes | Auth token |
| visitor_id | string | Conditional | Required for visitors |

### Events (Server -> Client)

| Event | Description | Payload |
|-------|-------------|---------|
| `message` | New message received | `{type, from, message, file?, conversation_id}` |
| `typing_start` | User started typing | `{type, visitor_id, name}` |
| `typing_stop` | User stopped typing | `{type, visitor_id}` |
| `user_joined` | New visitor connected | `{type, visitor_id, name}` |
| `user_left` | Visitor disconnected | `{type, visitor_id}` |
| `support_joined` | Support agent online | `{type, name}` |
| `support_left` | Support agent offline | `{type}` |
| `analysis` | AI analysis result | `{type, from, analysis}` |

### Events (Client -> Server)

| Event | Description | Payload |
|-------|-------------|---------|
| `message` | Send message | `{message, to?, file?}` |
| `typing_start` | Start typing | `{type: "typing_start"}` |
| `typing_stop` | Stop typing | `{type: "typing_stop"}` |
| `init` | Initialize visitor | `{type: "init", name}` |
| `get_state` | Get current state | `{type: "get_state"}` |

---

## Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `INVALID_CREDENTIALS` | 401 | Invalid username or password |
| `TOKEN_EXPIRED` | 401 | Access token has expired |
| `UNAUTHORIZED` | 401 | Missing or invalid authorization |
| `FORBIDDEN` | 403 | No access to this resource |
| `NOT_FOUND` | 404 | Resource not found |
| `VALIDATION_ERROR` | 400 | Invalid request parameters |
| `FILE_TOO_LARGE` | 400 | File exceeds size limit |
| `INVALID_FILE_TYPE` | 400 | File type not allowed |
| `RATE_LIMITED` | 429 | Too many requests |
| `SERVER_ERROR` | 500 | Internal server error |

---

## Rate Limits

| Endpoint | Limit |
|----------|-------|
| Authentication | 10 requests/minute |
| Messages | 60 requests/minute |
| File Upload | 20 requests/minute |
| Analysis | 30 requests/minute |
| Other endpoints | 100 requests/minute |

---

## Data Models

### User (Support Agent)
```json
{
  "id": "string",
  "username": "string",
  "email": "string",
  "role": "support_agent | admin",
  "site_ids": ["string"],
  "status": "online | away | offline",
  "created_at": "datetime",
  "last_seen": "datetime"
}
```

### Visitor
```json
{
  "id": "string",
  "visitor_id": "string",
  "name": "string",
  "email": "string?",
  "metadata": "object?",
  "site_id": "string",
  "status": "online | offline",
  "first_seen": "datetime",
  "last_seen": "datetime"
}
```

### Conversation
```json
{
  "id": "string",
  "site_id": "string",
  "visitor_id": "string",
  "assigned_agent_id": "string?",
  "status": "active | closed",
  "created_at": "datetime",
  "closed_at": "datetime?",
  "last_message_at": "datetime"
}
```

### Message
```json
{
  "id": "string",
  "conversation_id": "string",
  "from": "visitor | support",
  "sender_id": "string",
  "content": "string?",
  "type": "text | file | image",
  "file_id": "string?",
  "status": "sent | delivered | read",
  "created_at": "datetime"
}
```

### File
```json
{
  "id": "string",
  "filename": "string",
  "original_name": "string",
  "mime_type": "string",
  "size": "integer",
  "url": "string",
  "is_image": "boolean",
  "uploaded_by_type": "visitor | support",
  "uploaded_by_id": "string",
  "created_at": "datetime"
}
```
