# UserService - Seed Data

## Overview
The UserService automatically seeds the database with initial user data on first startup.

## Seeded Users

The following users are automatically created when the database is first initialized:

### 1. Admin User
- **Username:** `admin`
- **Email:** `admin@bookingsystem.com`
- **Password:** `Admin@123`
- **First Name:** Admin
- **Last Name:** User
- **Phone:** +1234567890
- **Status:** Active
- **Purpose:** System administrator account

### 2. John Doe
- **Username:** `john.doe`
- **Email:** `john.doe@example.com`
- **Password:** `Password@123`
- **First Name:** John
- **Last Name:** Doe
- **Phone:** +1234567891
- **Status:** Active
- **Purpose:** Test user account

### 3. Jane Smith
- **Username:** `jane.smith`
- **Email:** `jane.smith@example.com`
- **Password:** `Password@123`
- **First Name:** Jane
- **Last Name:** Smith
- **Phone:** +1234567892
- **Status:** Active
- **Purpose:** Test user account

### 4. Bob Johnson
- **Username:** `bob.johnson`
- **Email:** `bob.johnson@example.com`
- **Password:** `Password@123`
- **First Name:** Bob
- **Last Name:** Johnson
- **Phone:** +1234567893
- **Status:** Active
- **Purpose:** Test user account

### 5. Alice Williams
- **Username:** `alice.williams`
- **Email:** `alice.williams@example.com`
- **Password:** `Password@123`
- **First Name:** Alice
- **Last Name:** Williams
- **Phone:** +1234567894
- **Status:** Active
- **Purpose:** Test user account

## Usage

### Login Example

You can use any of the seeded users to test the authentication endpoints:

```bash
# Login as admin
POST /api/users/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin@123"
}

# Login as regular user
POST /api/users/login
Content-Type: application/json

{
  "username": "john.doe",
  "password": "Password@123"
}
```

## Implementation Details

### Seeder Logic
- **Location:** `Data/UserDbSeeder.cs`
- **Execution:** Runs automatically after database migrations in `Program.cs`
- **Idempotent:** Only seeds data if no users exist in the database
- **Password Hashing:** All passwords are hashed using BCrypt before storage

### Seeding Process
1. Application starts
2. Database migrations are applied
3. Seeder checks if any users exist
4. If database is empty, seed users are created
5. Success message is logged

## Security Notes

⚠️ **Important:** These are development/testing credentials. In a production environment:
- Change all default passwords immediately
- Use strong, unique passwords
- Consider removing or disabling test accounts
- Implement proper user management policies
- Use environment-specific seed data

## Customization

To modify the seed data, edit the `UserDbSeeder.cs` file in the `Data` folder. You can:
- Add more users
- Change default credentials
- Modify user properties
- Add role-based seeding (if roles are implemented)

## Troubleshooting

### Seed Not Running
If users are not being created:
1. Check application logs for errors
2. Verify database connection is working
3. Ensure migrations have been applied
4. Check if users already exist in the database

### Reset Seed Data
To re-run the seed:
1. Drop the Users table or entire database
2. Restart the application
3. Migrations and seed will run automatically
