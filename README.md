# Appointly

**Team:** Sam Bingham, Valeria Silva, Brenleigh Killpack, Ethan Seegmiller

## Project Description

Appointly is an appointment scheduling and management system designed for small businesses.

### Main Components

-   **Controllers/**: Handle CRUD and business logic for appointments, users, teams, authentication, and participant management.
-   **Models/**: Define entities for appointments, users, teams, and join tables for many-to-many relationships.
-   **AppointlyContext.cs**: Entity Framework DbContext, configures relationships and database access.
-   **Extensions/**: Helper methods for authorization and user ID extraction.
-   **Views/**: Razor views for appointments, users, teams, and dashboard.
-   **wwwroot/**: Static assets (CSS, JS, third-party libraries).
-   **appsettings.json**: App configuration (logging, hosts).
-   **Program.cs**: App entry point, configures services, authentication, and database.
-   **appointly.tests/**: Unit tests for controllers and authentication logic.

## Target Users

Small businesses

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- A Google account (used for login)

### Running the App
1. Clone the repository
```bash
   git clone https://github.com/your-username/appointly.git
   cd appointly
```
2. Run the app
```bash
   dotnet run
```
3. Open your browser and go to `http://localhost:5149`

---

## How to Use

### Logging In
- Click **Sign in with Google** on the landing page
- You will be redirected to the home calendar after signing in

### Viewing Appointments
- The **Home** page shows today's date, a summary of today's appointments, and a full monthly calendar
- Click the **Appointments** tab in the nav to see a full list of your appointments

### Creating an Appointment
- Click the **+** button in the bottom right corner of the calendar, or
- Click any date on the calendar to open the create form with that date pre-filled
- Fill in the title, description, start/end time, status, and optional team
- Click **Save Appointment**

### Editing an Appointment
- From the calendar, click an existing event to go to its details
- From the Appointments list, click **Edit** on any row

### Deleting an Appointment
- From the Appointments list, click **Delete** on any row
- Confirm the deletion on the next screen

### Teams
- Use the **Teams** tab to create and manage teams
- When creating an appointment you can optionally associate it with a team

---

## Notes
- All times are stored in UTC and converted to your local time for display
- Past appointments appear muted in both the calendar and the list view
