# EmployeeMvc — setup (email via Resend API)

## 1. Database migration

Package Manager Console:

    Add-Migration AddGroups
    Update-Database

Or CLI:

    dotnet ef migrations add AddGroups
    dotnet ef database update

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

## 3. Roles

Admin / Manager / Support / Employee. New registrations always receive Employee; an Admin assigns other roles from the Users page.

Employee records are linked by confirmed email address (user email = Employees table Email).
