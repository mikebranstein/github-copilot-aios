# Team Equipment Checkout Tracker

A beginner-friendly ASP.NET Core MVC application for tracking team equipment checkouts.

## Projects

| Project | Description |
|---|---|
| `src/EquipmentTracker.Web` | ASP.NET Core MVC web application |
| `tests/EquipmentTracker.Web.Tests` | xUnit unit tests for service logic |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Getting Started

### Restore dependencies

```bash
dotnet restore EquipmentTracker.slnx
```

### Build

```bash
dotnet build EquipmentTracker.slnx
```

### Run the tests

```bash
dotnet test EquipmentTracker.slnx
```

### Run the web app

```bash
dotnet run --project src/EquipmentTracker.Web
```

Then open your browser to `https://localhost:5001` (or the URL shown in the terminal).

## Features

- View all equipment with live availability status
- Add new equipment items
- Check out an available item to a borrower
- Return a checked-out item
- Client- and server-side validation
- 3 sample items seeded at startup (Laptop, Projector, Whiteboard Marker Set)

## Project Structure

```
EquipmentTracker.sln
src/
  EquipmentTracker.Web/
    Controllers/
      HomeController.cs         # Redirects / → /Equipment
      EquipmentController.cs    # Index, Create, Checkout, Return
    Models/
      EquipmentItem.cs
      CheckoutRecord.cs
    ViewModels/
      CreateEquipmentViewModel.cs
      CheckoutViewModel.cs
    Services/
      IEquipmentService.cs
      EquipmentService.cs       # In-memory singleton, seeded at startup
    Views/
      Equipment/
        Index.cshtml
        Create.cshtml
        Checkout.cshtml
      Shared/
        _Layout.cshtml
    Program.cs
tests/
  EquipmentTracker.Web.Tests/
    EquipmentServiceTests.cs    # 5 unit tests covering CRUD and checkout logic
```
