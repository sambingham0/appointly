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