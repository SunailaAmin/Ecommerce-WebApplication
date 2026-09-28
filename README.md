# ECommerce

A full-stack e-commerce application built with **ASP.NET Core 8 Web API** and **React + Vite**.

## Features

* JWT Authentication & Authorization
* Product & Category Management
* Inventory Tracking
* Shopping Cart
* Address Management
* Order Processing & Checkout
* Mock Payment Gateway
* SignalR Real-Time Notifications
* Caching with IMemoryCache
* Swagger API Documentation
* Unit & Integration Testing

## Tech Stack

### Backend

* ASP.NET Core 8
* Entity Framework Core 8
* SQL Server
* JWT Authentication
* SignalR
* xUnit, Moq, FluentAssertions

### Frontend

* React 18
* Vite
* React Router

## Project Structure

```text
src/
├── ECommerce.Domain          # Entities & domain models
├── ECommerce.Application     # Services, DTOs, interfaces
├── ECommerce.Infrastructure  # EF Core, repositories, JWT
└── ECommerce.API             # Controllers, middleware, Swagger

client/                       # React storefront
tests/                        # Unit & integration tests
```

## Prerequisites

* .NET 8 SDK
* SQL Server LocalDB
* Node.js 20+

## Setup

### Configure JWT Secret

```powershell
dotnet user-secrets --project src/ECommerce.API init
dotnet user-secrets --project src/ECommerce.API set "Jwt:Key" "your-secret-key"
```

### Run Backend

```powershell
dotnet run --project src/ECommerce.API
```

### Run Frontend

```powershell
cd client
npm install
npm run dev
```

## Swagger

Available in Development mode:

* `http://localhost:5145/swagger`
* `https://localhost:7236/swagger`

## Default Roles

* **Customer** – assigned during registration
* **Admin** – must be assigned manually

## Testing

```powershell
dotnet test
```

## Architecture

The application follows a **Clean Architecture / Layered Architecture** approach, separating Domain, Application, Infrastructure, and API layers for maintainability and scalability.
