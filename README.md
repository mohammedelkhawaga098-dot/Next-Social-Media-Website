# NEXT

A social networking application built with ASP.NET Core 10 MVC, Entity Framework Core (Code First), SQL Server, and SignalR. The interface uses a dark theme by default.

## Requirements

- .NET 10 SDK
- SQL Server available at `.` with Windows Authentication. The account running the application must be allowed to create a database (the default connection string uses this configuration).
- An internet connection at runtime. Bootstrap, Font Awesome, the SignalR client, and the Inter font are loaded from CDNs.

## Getting Started

```bash
cd NEXT
dotnet restore
dotnet run
```

At startup, the application automatically applies EF Core migrations (`Database.MigrateAsync`). This creates the **NEXT** database and its tables, or updates an existing schema.
Open the URL shown in the console (for example, `https://localhost:7xxx`) and create an account from the Register page.

## Database

- The connection string is configured in `appsettings.json`.
- The database name (`NEXT`) is added in `Program.cs` from `Database:Name`.
- If the installed SqlClient version does not recognize the `Command Timeout` keyword, the application removes it from the connection string and applies `CommandTimeout(0)` through EF Core.

## Project Structure

| Folder | Contents |
|---|---|
| `Models` | Entities for users, posts, comments, likes, friendships, follows, messages, and notifications |
| `Data` | `ApplicationDbContext` |
| `Migrations` | EF Core database migrations and the model snapshot |
| `Services` | Business logic for posts, friendships, chat, notifications, file uploads, and profiles |
| `ViewModels` | View models used by pages and partial views |
| `Controllers` | Account, Home, Posts, Profile, Friends, Messages, Notifications, Search, and Settings endpoints |
| `Hubs` | `NextHub` (SignalR) for presence updates and live message and notification events |
| `Views`, `wwwroot` | Razor UI, CSS, and JavaScript |

## Features

- Registration, login, and logout with ASP.NET Core Identity password hashing and account lockout after five failed attempts.
- User profiles with an avatar, cover image, bio, profile editing, and password changes.
- Text and image posts, editing and deletion by the post owner, likes, comments, shares, newest-first ordering, and a “Load more” feed.
- Friend requests that can be sent, accepted, declined, cancelled, or removed; duplicate and self-requests are prevented. Users can also follow people and search by name or username.
- Real-time SignalR messaging between friends, including images, online status, unread counts, and the latest message. Messages are persisted in SQL Server.
- Live notifications for friend requests and acceptances, likes, comments, shares, and messages.
- Dark theme by default. Users can switch to light mode from **Settings → Appearance**; the preference is saved per user in the database.
- Responsive layouts with a sidebar on large screens, an icon navigation bar on tablets, and a bottom navigation bar on phones.

## Security

- Anti-forgery validation for all POST requests, using a global filter and the `RequestVerificationToken` header for fetch requests.
- A global authorization policy requires authentication. Resource-based authorization restricts post and comment edits and deletions to their owners.
- Image uploads allow JPG, PNG, GIF, and WebP files up to 5 MB. The server checks the actual file signature and assigns a randomized filename.
- Database queries use EF Core parameterization, and Razor output is encoded automatically.
- Conversations are private: users can message friends only, and message access is based on the authenticated cookie identity.

## Notes

- The application does not include sample data; all content is read from the database. Create at least two accounts and make them friends to try messaging.
- Use HTTPS in production and store the connection string outside the repository.
