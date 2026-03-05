# MiniDrive - Quick Start Guide

## 🚀 Get Started in 5 Minutes

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose
- SQL Server (via Docker)
- Redis (via Docker)

### Step 1: Clone & Setup
```bash
cd c:\Users\admin\Documents\Code\.net\MiniDrive
```

### Step 2: Start Infrastructure (Database & Cache)
```bash
docker-compose up -d

# Wait ~10 seconds for SQL Server to be ready
# Verify: docker-compose ps
```

### Step 3: Build Solution
```bash
cd src
dotnet build --configuration Release
```

### Step 4: Run Services
```bash
# Option A: Run Gateway (auto-starts dependent services)
dotnet run --project MiniDrive.Gateway.Api

# Option B: Run individual services in separate terminals
dotnet run --project MiniDrive.Identity.Api
dotnet run --project MiniDrive.Files.Api
dotnet run --project MiniDrive.Folders.Api
dotnet run --project MiniDrive.Quota.Api
dotnet run --project MiniDrive.Audit.Api
dotnet run --project MiniDrive.Sharing.Api
```

### Step 5: Test APIs
```bash
# Gateway is ready at http://localhost:5000

# Test file upload
curl -X POST http://localhost:5000/api/file/upload \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@yourfile.pdf"

# List files (with pagination)
curl http://localhost:5000/api/file?pageNumber=1&pageSize=20 \
  -H "Authorization: Bearer YOUR_TOKEN"

# View API docs
open http://localhost:5000/swagger
```

---

## 📊 Service Ports

| Service | Port | Health Check |
|---------|------|--------------|
| Gateway | 5000 | http://localhost:5000/health |
| Identity | 5001 | http://localhost:5001/health |
| Files | 5002 | http://localhost:5002/health |
| Folders | 5003 | http://localhost:5003/health |
| Quota | 5004 | http://localhost:5004/health |
| Audit | 5005 | http://localhost:5005/health |
| Sharing | 5008 | http://localhost:5008/health |

---

## 🔐 Authentication

### Get JWT Token
```bash
# Create test user (if not exists)
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Pass123!"}'

# Login
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Pass123!"}'

# Response includes: { "token": "eyJhbGc...", "user": {...} }
```

### Use Token
```bash
TOKEN="eyJhbGc..."

curl http://localhost:5000/api/file \
  -H "Authorization: Bearer $TOKEN"
```

---

## 📁 Key Features to Try

### File Management
```bash
# Upload file
curl -X POST http://localhost:5000/api/file/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@document.pdf" \
  -F "description=My document"

# List files (with pagination)
curl "http://localhost:5000/api/file?pageNumber=1&pageSize=10&search=document" \
  -H "Authorization: Bearer $TOKEN"

# Get file metadata
curl http://localhost:5000/api/file/{fileId} \
  -H "Authorization: Bearer $TOKEN"

# Download file
curl http://localhost:5000/api/file/{fileId}/download \
  -H "Authorization: Bearer $TOKEN" \
  -o downloaded_file.pdf
```

### Folder Organization
```bash
# Create folder
curl -X POST http://localhost:5000/api/folder \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"My Folder","description":"For documents"}'

# List folders (paginated)
curl "http://localhost:5000/api/folder?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"

# Get folder path (breadcrumb)
curl http://localhost:5000/api/folder/{folderId}/path \
  -H "Authorization: Bearer $TOKEN"
```

### File Sharing
```bash
# Create share link
curl -X POST http://localhost:5000/api/share \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "resourceId":"{fileId}",
    "resourceType":"File",
    "isPublic":true,
    "expiration":"2026-03-14"
  }'

# List user shares (paginated)
curl http://localhost:5000/api/share \
  -H "Authorization: Bearer $TOKEN"

# Access public share (no auth needed)
curl http://localhost:5000/api/share/{shareToken}
```

---

## 🔍 Monitoring & Debugging

### View Logs
```bash
# Docker logs
docker-compose logs -f sqlserver
docker-compose logs -f redis

# Application logs (in terminal)
# Check console output where services are running
```

### Database Inspection
```bash
# Connect to SQL Server
docker exec -it minidrive-sqlserver sqlcmd -S localhost -U sa -P YourStrong!Pass123

# List databases
SELECT name FROM sys.databases;

# Query users table
USE Identity;
SELECT * FROM Users;
```

### Redis Cache
```bash
# Connect to Redis
docker exec -it minidrive-redis redis-cli

# View cache keys
KEYS *

# Get cached value
GET "identity:token:HASH_HERE"

# Clear cache
FLUSHALL
```

### Health Checks
```bash
# Overall gateway health
curl http://localhost:5000/health

# Detailed health
curl http://localhost:5000/health | jq '.'

# Database check
curl http://localhost:5000/health/sql

# Cache check
curl http://localhost:5000/health/redis
```

---

## 🐛 Troubleshooting

### Services Not Starting
```bash
# Check container status
docker-compose ps

# Restart containers
docker-compose restart

# View logs
docker-compose logs --tail=50
```

### Connection Errors
```bash
# SQL Server not ready - wait 20 seconds:
docker-compose ps sqlserver
# Wait for "running" status

# Redis not available:
docker-compose logs redis
```

### Port Already in Use
```bash
# Find process on port
netstat -ano | findstr :5000

# Kill process (Windows)
taskkill /PID <PID> /F

# Or change port in appsettings.json
```

### Build Failures
```bash
# Full clean rebuild
dotnet clean
dotnet build --configuration Release

# Restore packages
dotnet restore
```

---

## 📚 Learn More

- **Full Architecture**: See `PROJECT_OVERVIEW.md`
- **Security Details**: See `SECURITY_FIXES.md`
- **Code Review**: See `CODE_REVIEW.md`
- **API Documentation**: Visit http://localhost:5000/swagger
- **Microservices Guide**: See `MICROSERVICES_SETUP.md`

---

## 🎯 Next Steps

1. ✅ Verify all services running (`/health` endpoints)
2. ✅ Create test user and get JWT token
3. ✅ Upload a file and verify storage
4. ✅ Create folders and organize files
5. ✅ Create share links for collaboration
6. ✅ Monitor audit logs for all actions

**Need Help?** Check the error logs and health endpoints first - they'll tell you what's wrong.

---

**Last Updated**: February 14, 2026 | **Framework**: .NET 10
