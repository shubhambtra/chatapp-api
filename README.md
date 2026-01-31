# ChatApp API

ASP.NET Core Web API for the ChatApp real-time customer support chat application.

## Prerequisites

- .NET 8.0 SDK
- SQL Server (LocalDB, Express, or full edition)
- Visual Studio 2022 or VS Code

## Getting Started

### 1. Clone and Navigate

```bash
cd API/ChatApp.API
```

### 2. Update Configuration

Edit `appsettings.json` to configure:

- **ConnectionStrings:DefaultConnection** - Your SQL Server connection string
- **JwtSettings:Secret** - A secure secret key (min 32 characters)
- **OpenAI:ApiKey** - Your OpenAI API key for AI features
- **Stripe:SecretKey** - Your Stripe secret key for payments

### 3. Run Migrations (Optional)

If using Entity Framework migrations:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Or use the provided `database_schema.sql` file to create the database manually.

### 4. Run the Application

```bash
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

## Project Structure

```
ChatApp.API/
├── Controllers/          # API endpoints
├── Data/
│   ├── Configurations/   # Entity Framework configurations
│   └── ApplicationDbContext.cs
├── Hubs/                 # SignalR hubs
├── Middleware/           # Custom middleware
├── Models/
│   ├── DTOs/            # Data transfer objects
│   └── Entities/        # Database entities
├── Services/
│   ├── Interfaces/      # Service interfaces
│   └── Implementations/ # Service implementations
├── Program.cs           # Application entry point
└── appsettings.json     # Configuration
```

## API Endpoints

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/me` - Get current user

### Sites
- `GET /api/sites` - List user's sites
- `POST /api/sites` - Create a new site
- `GET /api/sites/{id}` - Get site details
- `PUT /api/sites/{id}` - Update site
- `DELETE /api/sites/{id}` - Delete site

### Visitors
- `GET /api/sites/{siteId}/visitors` - List visitors
- `POST /api/sites/{siteId}/visitors` - Create visitor
- `GET /api/sites/{siteId}/visitors/{id}` - Get visitor

### Conversations
- `GET /api/sites/{siteId}/conversations` - List conversations
- `POST /api/sites/{siteId}/conversations` - Start conversation
- `GET /api/sites/{siteId}/conversations/{id}` - Get conversation
- `POST /api/sites/{siteId}/conversations/{id}/close` - Close conversation

### Messages
- `GET /api/conversations/{conversationId}/messages` - Get messages
- `POST /api/conversations/{conversationId}/messages` - Send message

### Files
- `POST /api/files/upload` - Upload file
- `GET /api/files/{id}` - Download file

### Subscriptions
- `GET /api/subscriptions/plans` - List subscription plans
- `POST /api/subscriptions/sites/{siteId}` - Create subscription
- `GET /api/subscriptions/sites/{siteId}` - Get current subscription

### Analytics
- `GET /api/sites/{siteId}/analytics/dashboard` - Dashboard stats
- `GET /api/sites/{siteId}/analytics/conversations` - Conversation analytics
- `GET /api/sites/{siteId}/analytics/agents` - Agent performance

## SignalR Hub

Connect to `/hubs/chat` for real-time messaging.

### Events
- `NewMessage` - New message received
- `UserTyping` - User started typing
- `ConversationUpdated` - Conversation status changed
- `AgentOnline/Offline` - Agent status changed
- `VisitorOnline/Offline` - Visitor status changed

### Methods
- `JoinConversation(conversationId)` - Join a conversation room
- `SendMessage(conversationId, content, messageType)` - Send a message
- `StartTyping/StopTyping(conversationId)` - Typing indicators
- `MarkAsRead(conversationId, messageIds)` - Mark messages as read

## Environment Variables

For production, set these environment variables:

```
ConnectionStrings__DefaultConnection=<connection-string>
JwtSettings__Secret=<jwt-secret>
OpenAI__ApiKey=<openai-api-key>
Stripe__SecretKey=<stripe-secret-key>
Stripe__WebhookSecret=<stripe-webhook-secret>
```

## License

MIT License
