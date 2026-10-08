# EmployeeMvc — setup (email via Resend API)

## 1. Database migration

The app applies checked-in EF Core migrations automatically on startup. Add new schema changes as migrations and deploy them with the app.

## 2. Configure real email delivery without SMTP

This project sends email through Resend's HTTPS API. Create a free Resend account and an API key, then add a sending domain and verify it in Resend. The address configured as `SenderEmail` must belong to a verified sending domain. Add the DNS records Resend provides at your domain registrar.

For local development, from the EmployeeMvc project directory run:

    dotnet user-secrets set "EmailSettings:ResendApiKey" "re_your_api_key"
    dotnet user-secrets set "EmailSettings:SenderEmail" "no-reply@your-verified-domain.com"
    dotnet user-secrets set "EmailSettings:SenderName" "Employee Management System"

For Docker/server deployment, set environment variables:

    EmailSettings__ResendApiKey=re_your_api_key
    EmailSettings__SenderEmail=no-reply@your-verified-domain.com
    EmailSettings__SenderName=Employee Management System
    App__PublicBaseUrl=https://your-domain.com
    SeedAdmin__Email=admin@yourcompany.com
    SeedAdmin__Password=your-strong-password

Never put API keys or passwords in source code or committed settings files. Resend's free plan currently allows 3,000 emails per month, with a 100-email daily limit. Provider limits and sender-domain verification still apply.

## 3. Roles and employees

Admin / Manager / Support / Employee. Public registration creates only an Employee account. When creating an employee, an Admin can choose Employee, Support, or Manager; a Manager can create Employee accounts only. The flow creates the Identity account and employee record and adds the account to one of that Admin's groups. Admins see only their own groups and their members' employee records. Employees see their own profile, tasks, and groups they belong to.

Admin-created employee accounts start with an unconfirmed email address. The Employee confirms it with the code sent during their first login. The account is linked directly to its Employee record; older records can still be matched by confirmed email.

## 4. Create the first Admin account

The first Admin is provisioned separately from public registration. From the project directory, set the account credentials with User Secrets:

    dotnet user-secrets set "SeedAdmin:Email" "admin@yourcompany.com"
    dotnet user-secrets set "SeedAdmin:Password" "your-strong-password"

Restart the app. On startup, it creates this Admin account if it does not exist. If the email already belongs to a confirmed account, that account is granted the Admin role and keeps its existing password. An unconfirmed existing account is not promoted automatically.

After signing in as Admin, open **მომხმარებლები** and search by all or part of the registered user's email. The Admin can change roles for confirmed accounts in their own groups, or for unassigned public registrations. Users who belong to another Admin's group are not listed. The Admin role can be granted only to an unassigned account.

## 5. Resend test mode

The API key can be configured correctly while Resend still rejects a recipient in test mode. To send verification codes to arbitrary users, verify a domain in Resend, set `EmailSettings:SenderEmail` to an address on that domain, and use the same API key. Check Resend's Domains and Logs pages when a send is rejected.
