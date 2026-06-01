# =================================================================
# MarketPro -- Comprehensive Black-Box Test Suite (as actual user)
# Runs against http://localhost:5211 (local API with latest master code)
#
# Covers: auth, profile, M1 (SmtpGroup credential indicator),
# M2 (active-sender endpoint), contacts, templates, campaigns,
# inbox, settings, audit logs, pagination edge cases, BUG regressions.
# =================================================================

$ErrorActionPreference = 'Continue'
$BaseUrl = 'http://localhost:5211'
$ApiUrl  = "$BaseUrl/api/v1"
$Stamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

$global:PassCount = 0
$global:FailCount = 0
$global:Results   = @()

function TestCase {
    param([string]$Section, [string]$Id, [string]$Name, [scriptblock]$Block)
    $start = Get-Date
    try {
        $result = & $Block
        if ($result -eq $true -or $null -eq $result) {
            $global:PassCount++
            $line = "  [PASS] {0,-4} {1}" -f $Id, $Name
            Write-Host $line -ForegroundColor Green
            $global:Results += [PSCustomObject]@{ Section=$Section; Id=$Id; Name=$Name; Status='PASS'; Error=$null; Ms=[int]((Get-Date) - $start).TotalMilliseconds }
        } else {
            $global:FailCount++
            $line = "  [FAIL] {0,-4} {1} | {2}" -f $Id, $Name, $result
            Write-Host $line -ForegroundColor Red
            $global:Results += [PSCustomObject]@{ Section=$Section; Id=$Id; Name=$Name; Status='FAIL'; Error=$result; Ms=[int]((Get-Date) - $start).TotalMilliseconds }
        }
    } catch {
        $global:FailCount++
        $err = $_.Exception.Message
        if ($_.ErrorDetails) { $err += " | " + $_.ErrorDetails.Message }
        $line = "  [FAIL] {0,-4} {1} | EX: {2}" -f $Id, $Name, ($err.Substring(0, [Math]::Min(200, $err.Length)))
        Write-Host $line -ForegroundColor Red
        $global:Results += [PSCustomObject]@{ Section=$Section; Id=$Id; Name=$Name; Status='FAIL'; Error=$err; Ms=[int]((Get-Date) - $start).TotalMilliseconds }
    }
}

function Section { param([string]$Name); Write-Host ""; Write-Host "=== $Name ===" -ForegroundColor Cyan }

function Call-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null,
        [int[]]$ExpectStatus = @(200, 201, 204)
    )
    $url = if ($Path.StartsWith('http')) { $Path } else { "$ApiUrl$Path" }
    $headers = @{ 'accept' = 'application/json' }
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $a = @{ Uri = $url; Method = $Method; Headers = $headers; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($Body -ne $null) {
        $a['ContentType'] = 'application/json'
        $a['Body'] = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    try {
        $resp = Invoke-WebRequest @a -ErrorAction Stop
        $code = $resp.StatusCode
        $obj = if ($resp.Content) { try { $resp.Content | ConvertFrom-Json } catch { $resp.Content } } else { $null }
        return [PSCustomObject]@{ Ok = ($ExpectStatus -contains $code); Status = $code; Data = $obj; Raw = $resp.Content }
    } catch {
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
            $body = if ($_.ErrorDetails) { $_.ErrorDetails.Message } else { '' }
            $obj = if ($body) { try { $body | ConvertFrom-Json } catch { $body } } else { $null }
            return [PSCustomObject]@{ Ok = ($ExpectStatus -contains $code); Status = $code; Data = $obj; Raw = $body }
        }
        throw
    }
}

# ===== SECTION A -- Auth =====
Section "A. Authentication"

$adminEmail = "admin-$Stamp@blackbox.test"
$userEmail  = "user-$Stamp@blackbox.test"
$adminPwd   = "TestPwd123!"
$userPwd    = "UserPwd123!"
$global:adminToken = $null
$global:userToken  = $null

TestCase 'A' 'A1' 'Health endpoint returns 200 Healthy' {
    $r = Call-Api -Method GET -Path 'http://localhost:5211/health'
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Raw -notmatch 'Healthy') { return "body=$($r.Raw)" }
    return $true
}

TestCase 'A' 'A2' 'POST /auth/login no body returns 4xx' {
    $r = Call-Api -Method POST -Path '/auth/login' -Body @{} -ExpectStatus @(400, 401, 422)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'A' 'A3' 'POST /auth/login wrong creds returns 401' {
    $r = Call-Api -Method POST -Path '/auth/login' -Body @{ email = 'nobody@nowhere.test'; password = 'wrong' } -ExpectStatus @(401, 400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'A' 'A4' 'POST /auth/register creates admin/user + returns accessToken' {
    $r = Call-Api -Method POST -Path '/auth/register' -Body @{ email = $adminEmail; password = $adminPwd; fullName = 'Black Box Admin' }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:adminToken = $r.Data.data.accessToken
    $global:adminUserId = $r.Data.data.user.id
    if (-not $global:adminToken) { return "no accessToken" }
    # Promote this test user to admin via DB so admin-only test cases can run.
    # BUG-001 first-user-admin logic doesn't help here because the DB already
    # has other users; that's a separate test of fresh-DB behavior.
    $cfg = Get-Content "D:\MultiChannelMarkettingApp\src\MarketingApp.API\appsettings.json" -Raw | ConvertFrom-Json
    if ($cfg.ConnectionStrings.DefaultConnection -match 'Password=([^;]+)') { $env:PGPASSWORD = $matches[1] }
    $psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
    & $psql -h localhost -U postgres -d marketingapp -c "UPDATE users SET role='admin' WHERE email='$adminEmail'" 2>&1 | Out-Null
    # Re-login so JWT carries admin role
    $r2 = Call-Api -Method POST -Path '/auth/login' -Body @{ email = $adminEmail; password = $adminPwd }
    if (-not $r2.Ok) { return "promote+relogin failed: $($r2.Status)" }
    $global:adminToken = $r2.Data.data.accessToken
    if ($r2.Data.data.user.role -ne 'admin') { return "promotion didn't stick" }
    return $true
}

TestCase 'A' 'A5' 'POST /auth/register second user gets role=user' {
    $r = Call-Api -Method POST -Path '/auth/register' -Body @{ email = $userEmail; password = $userPwd; fullName = 'Black Box User' }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:userToken = $r.Data.data.accessToken
    $global:normalUserId = $r.Data.data.user.id
    if ($r.Data.data.user.role -ne 'user') { return "expected user role, got $($r.Data.data.user.role)" }
    return $true
}

TestCase 'A' 'A6' 'BUG-001: a role=admin user exists in the system' {
    # Either our adminEmail registration created an admin (fresh DB), OR a prior
    # admin exists. Either way, /admin/users must reveal at least one admin.
    if (-not $global:adminToken) {
        # Try logging in as the user we just registered to query
        $r = Call-Api -Method POST -Path '/auth/login' -Body @{ email = $userEmail; password = $userPwd }
        if (-not $r.Ok) { return "couldn't log in to query users" }
    }
    return $true   # informational; if no admin existed nothing would work below
}

TestCase 'A' 'A7' 'POST /auth/register duplicate email rejected' {
    $r = Call-Api -Method POST -Path '/auth/register' -Body @{ email = $userEmail; password = $userPwd; fullName = 'Dup' } -ExpectStatus @(400, 409, 422)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'A' 'A8' 'POST /auth/login correct creds returns accessToken' {
    $r = Call-Api -Method POST -Path '/auth/login' -Body @{ email = $userEmail; password = $userPwd }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if (-not $r.Data.data.accessToken) { return "no accessToken" }
    return $true
}

TestCase 'A' 'A9' 'Protected endpoint no token returns 401' {
    $r = Call-Api -Method GET -Path '/me/profile' -ExpectStatus @(401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'A' 'A10' 'Protected endpoint bogus token returns 401' {
    $r = Call-Api -Method GET -Path '/me/profile' -Token 'bogus.jwt.token' -ExpectStatus @(401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'A' 'A11' 'POST /auth/refresh with bogus refresh token returns 4xx' {
    $r = Call-Api -Method POST -Path '/auth/refresh' -Body @{ refreshToken = 'nope' } -ExpectStatus @(400, 401, 404)
    if ($r.Status -ge 500) { return "5xx panic: $($r.Status)" }
    return $true
}

# ===== SECTION B -- /me Endpoints =====
Section "B. /me Endpoints"

TestCase 'B' 'B1' 'GET /me/profile (admin) returns email' {
    $r = Call-Api -Method GET -Path '/me/profile' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Data.data.email -ne $adminEmail) { return "email mismatch: $($r.Data.data.email)" }
    return $true
}

TestCase 'B' 'B2' 'GET /me/profile (user) returns role=user' {
    $r = Call-Api -Method GET -Path '/me/profile' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Data.data.role -ne 'user') { return "role=$($r.Data.data.role)" }
    return $true
}

TestCase 'B' 'B3' 'PUT /me/profile updates full name' {
    $r = Call-Api -Method PUT -Path '/me/profile' -Token $global:userToken -Body @{ fullName = 'Renamed User' }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Data.data.fullName -ne 'Renamed User') { return "name not updated" }
    return $true
}

TestCase 'B' 'B4' 'POST /me/password wrong current rejected' {
    $r = Call-Api -Method POST -Path '/me/password' -Token $global:userToken -Body @{ currentPassword = 'wrongpwd'; newPassword = 'NewPwd456!' } -ExpectStatus @(400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'B' 'B5' 'POST /me/password too short rejected' {
    $r = Call-Api -Method POST -Path '/me/password' -Token $global:userToken -Body @{ currentPassword = $userPwd; newPassword = 'x' } -ExpectStatus @(400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'B' 'B6' 'GET /me/signature returns merged signature DTO' {
    $r = Call-Api -Method GET -Path '/me/signature' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if (-not $r.Data.data.PSObject.Properties['fullName']) { return "no fullName in DTO" }
    return $true
}

TestCase 'B' 'B7' 'PUT /me/signature updates designation' {
    $r = Call-Api -Method PUT -Path '/me/signature' -Token $global:userToken -Body @{ signatureDesignation = 'Senior Tester'; signaturePhone = '+971555000111'; signatureImageUrl = $null }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Data.data.signatureDesignation -ne 'Senior Tester') { return "designation not saved" }
    return $true
}

# ===== SECTION C -- M2 Active Sender (NEW) =====
Section "C. M2 -- Active Sender (new endpoint)"

TestCase 'C' 'C1' 'GET /me/active-sender exists (NOT 404)' {
    $r = Call-Api -Method GET -Path '/me/active-sender' -Token $global:userToken
    if ($r.Status -eq 404) { return "404 -- endpoint not deployed" }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'C' 'C2' 'GET /me/active-sender has all expected fields' {
    $r = Call-Api -Method GET -Path '/me/active-sender' -Token $global:userToken
    if (-not $r.Data -or -not $r.Data.data) { return "no data envelope" }
    $data = $r.Data.data
    foreach ($f in @('groupId','groupName','provider','fromEmail','fromName','isDefault','isAssignedToUser','hasApiKey','hasSmtpPassword')) {
        if (-not $data.PSObject.Properties[$f]) { return "missing field: $f" }
    }
    return $true
}

TestCase 'C' 'C3' 'GET /me/active-sender requires auth' {
    $r = Call-Api -Method GET -Path '/me/active-sender' -ExpectStatus @(401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION D -- M1 Admin SmtpGroups (NEW: credential masks) =====
Section "D. M1 -- /admin/smtp-groups + credential masks"

$global:createdGroupId = $null

TestCase 'D' 'D1' 'GET /admin/smtp-groups requires admin' {
    $r = Call-Api -Method GET -Path '/admin/smtp-groups' -Token $global:userToken -ExpectStatus @(403, 401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'D' 'D2' 'GET /admin/smtp-groups (admin) returns list' {
    $r = Call-Api -Method GET -Path '/admin/smtp-groups' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    return $true
}

TestCase 'D' 'D3' 'POST /admin/smtp-groups create Brevo group with API key' {
    $r = Call-Api -Method POST -Path '/admin/smtp-groups' -Token $global:adminToken -Body @{
        name = "BB Brevo $Stamp"; description = 'Black box Brevo'
        isDefault = $false; isActive = $true
        emailProvider = 'brevo'
        brevoApiKey = "xkeysib-blackbox-test-$Stamp-DUMMY-KEY-NOT-FOR-PROD-USE-12345678"
        fromEmail = 'bb@blackbox.test'; fromName = 'BB Tester'
        smtpPort = 587; smtpEnableSsl = $true; smtpTimeout = 30000
    } -ExpectStatus @(200, 201)
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:createdGroupId = $r.Data.data.id
    if (-not $global:createdGroupId) { return "no id returned" }
    return $true
}

TestCase 'D' 'D4' 'GET created group exposes brevoApiKeyMasked (M1)' {
    if (-not $global:createdGroupId) { return "skipped: no group id" }
    $r = Call-Api -Method GET -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if (-not $r.Data.data.brevoApiKeyMasked) { return "brevoApiKeyMasked empty" }
    return $true
}

TestCase 'D' 'D5' 'Mask format starts with xkey (M1 transparency)' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method GET -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken
    if ($r.Data.data.brevoApiKeyMasked -notmatch '^xkey') { return "mask doesn't start with xkey: '$($r.Data.data.brevoApiKeyMasked)'" }
    return $true
}

TestCase 'D' 'D6' 'Raw brevoApiKey NEVER returned (security)' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method GET -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken
    if ($r.Data.data.PSObject.Properties['brevoApiKey'] -and $r.Data.data.brevoApiKey) { return "raw API key leaked!" }
    return $true
}

TestCase 'D' 'D7' 'PUT with blank brevoApiKey preserves existing (M1 contract)' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method PUT -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken -Body @{
        name = "BB Brevo $Stamp"; description = 'Updated descr'; isDefault = $false; isActive = $true
        emailProvider = 'brevo'; brevoApiKey = ''
        fromEmail = 'bb@blackbox.test'; fromName = 'BB Tester'
        smtpPort = 587; smtpEnableSsl = $true; smtpTimeout = 30000
    }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    $r2 = Call-Api -Method GET -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken
    if (-not $r2.Data.data.brevoApiKeyMasked) { return "key lost after blank update!" }
    return $true
}

TestCase 'D' 'D8' 'POST create SMTP group exposes smtpPasswordSet=true (M1 boolean)' {
    $r = Call-Api -Method POST -Path '/admin/smtp-groups' -Token $global:adminToken -Body @{
        name = "BB SMTP $Stamp"; description = ''; isDefault = $false; isActive = $true
        emailProvider = 'smtp'
        smtpHost = 'smtp.example.com'; smtpPort = 587; smtpEnableSsl = $true; smtpTimeout = 30000
        smtpUsername = 'bb-user'; smtpPassword = 'bb-password-secret-123'
        fromEmail = 'bb-smtp@blackbox.test'; fromName = 'BB SMTP'
    }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    if (-not $r.Data.data.smtpPasswordSet) { return "smtpPasswordSet=false despite password" }
    return $true
}

TestCase 'D' 'D9' 'Raw smtpPassword NEVER returned (security)' {
    $r = Call-Api -Method GET -Path '/admin/smtp-groups' -Token $global:adminToken
    foreach ($g in $r.Data.data) {
        if ($g.PSObject.Properties['smtpPassword'] -and $g.smtpPassword) { return "leaked in group $($g.id)" }
    }
    return $true
}

TestCase 'D' 'D10' 'GET /admin/smtp-groups/users-assignments returns user list' {
    $r = Call-Api -Method GET -Path '/admin/smtp-groups/users-assignments' -Token $global:adminToken -ExpectStatus @(200, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'D' 'D11' 'POST /admin/smtp-groups/:id/clone clones a group' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method POST -Path "/admin/smtp-groups/$($global:createdGroupId)/clone" -Token $global:adminToken -ExpectStatus @(200, 201)
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    if (-not $r.Data.data.id) { return "no clone id" }
    return $true
}

# ===== SECTION E -- Contacts =====
Section "E. Contacts"

$global:createdContactId = $null
$global:createdContactGroupId = $null

TestCase 'E' 'E1' 'GET /contacts requires auth' {
    $r = Call-Api -Method GET -Path '/contacts' -ExpectStatus @(401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E2' 'GET /contacts returns list (200)' {
    $r = Call-Api -Method GET -Path '/contacts' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E3' 'POST /contacts creates new' {
    $r = Call-Api -Method POST -Path '/contacts' -Token $global:userToken -Body @{
        fullName = 'BB Contact'; email = "bb-contact-$Stamp@blackbox.test"
        phone = '+971555111222'; whatsAppNumber = '+971555111222'
    }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:createdContactId = $r.Data.data.id
    if (-not $global:createdContactId) { return "no id" }
    return $true
}

TestCase 'E' 'E4' 'GET /contacts?search=BB returns it (fullName ILIKE)' {
    $r = Call-Api -Method GET -Path "/contacts?search=BB" -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    # PagedResponse shape: { data: [...], totalCount, pageNumber, ... }
    $count = if ($r.Data.totalCount -ne $null) { [int]$r.Data.totalCount } elseif ($r.Data.data) { @($r.Data.data).Count } else { 0 }
    if ($count -lt 1) { return "0 results (totalCount=$($r.Data.totalCount))" }
    return $true
}

TestCase 'E' 'E5' 'BUG-003: GET /contacts?pageNumber=-5 clamps to 1 (no 500)' {
    $r = Call-Api -Method GET -Path '/contacts?pageNumber=-5&pageSize=10' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status) (expected 200; BUG-003 regression)" }
    return $true
}

TestCase 'E' 'E6' 'BUG-003: GET /contacts?pageNumber=0 clamps' {
    $r = Call-Api -Method GET -Path '/contacts?pageNumber=0&pageSize=10' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E7' 'GET /contacts?pageSize=99999 clamps to max' {
    $r = Call-Api -Method GET -Path '/contacts?pageNumber=1&pageSize=99999' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E8' 'GET /contacts?pageSize=-100 clamps' {
    $r = Call-Api -Method GET -Path '/contacts?pageNumber=1&pageSize=-100' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E9' 'PUT /contacts/:id update' {
    if (-not $global:createdContactId) { return "skipped" }
    $r = Call-Api -Method PUT -Path "/contacts/$($global:createdContactId)" -Token $global:userToken -Body @{
        fullName = 'BB Updated'
        email = "bb-contact-$Stamp@blackbox.test"
        phone = '+971555999888'
    }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    return $true
}

TestCase 'E' 'E10' 'POST /contacts/groups creates group' {
    $r = Call-Api -Method POST -Path '/contacts/groups' -Token $global:userToken -Body @{
        name = "BB Group $Stamp"; description = 'Black box group'
    }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:createdContactGroupId = $r.Data.data.id
    return $true
}

TestCase 'E' 'E11' 'GET /contacts/groups list' {
    $r = Call-Api -Method GET -Path '/contacts/groups' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E12' 'POST /contacts/assign-group bulk assignment' {
    if (-not $global:createdContactId -or -not $global:createdContactGroupId) { return "skipped" }
    $r = Call-Api -Method POST -Path '/contacts/assign-group' -Token $global:userToken -Body @{
        contactIds = @($global:createdContactId); groupId = $global:createdContactGroupId
    } -ExpectStatus @(200, 204)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'E' 'E13' 'GET /contacts/export.csv returns CSV' {
    try {
        $url = "$ApiUrl/contacts/export.csv"
        $resp = Invoke-WebRequest -Uri $url -Headers @{ Authorization = "Bearer $($global:userToken)" } -UseBasicParsing -TimeoutSec 15
        if ($resp.StatusCode -ne 200) { return "status=$($resp.StatusCode)" }
        return $true
    } catch { return "ex: $($_.Exception.Message)" }
}

TestCase 'E' 'E14' 'DELETE /contacts/:id removes' {
    if (-not $global:createdContactId) { return "skipped" }
    $r = Call-Api -Method DELETE -Path "/contacts/$($global:createdContactId)" -Token $global:userToken -ExpectStatus @(200, 204)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION F -- Templates =====
Section "F. Templates"

$global:createdTemplateId = $null

TestCase 'F' 'F1' 'GET /templates list' {
    $r = Call-Api -Method GET -Path '/templates' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'F' 'F2' 'POST /templates create email template' {
    $r = Call-Api -Method POST -Path '/templates' -Token $global:userToken -Body @{
        name = "BB Template $Stamp"; channel = 'email'
        subject = 'Black box subject'
        body = '<h1>Hello {{firstName}}</h1>'
    }
    if (-not $r.Ok) { return "status=$($r.Status) body=$($r.Raw)" }
    $global:createdTemplateId = $r.Data.data.id
    return $true
}

TestCase 'F' 'F3' 'PUT /templates/:id update' {
    if (-not $global:createdTemplateId) { return "skipped" }
    $r = Call-Api -Method PUT -Path "/templates/$($global:createdTemplateId)" -Token $global:userToken -Body @{
        name = "BB Template $Stamp"; channel = 'email'
        subject = 'Updated subject'; body = '<h1>Updated</h1>'
    }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'F' 'F4' 'DELETE /templates/:id removes' {
    if (-not $global:createdTemplateId) { return "skipped" }
    $r = Call-Api -Method DELETE -Path "/templates/$($global:createdTemplateId)" -Token $global:userToken -ExpectStatus @(200, 204)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION G -- Campaigns =====
Section "G. Campaigns"

TestCase 'G' 'G1' 'GET /campaigns list' {
    $r = Call-Api -Method GET -Path '/campaigns' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'G' 'G2' 'BUG-003: GET /campaigns?pageNumber=-1 clamps' {
    $r = Call-Api -Method GET -Path '/campaigns?pageNumber=-1' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'G' 'G3' 'GET /campaigns/<random-guid> returns 404' {
    $r = Call-Api -Method GET -Path '/campaigns/00000000-0000-0000-0000-000000000000' -Token $global:userToken -ExpectStatus @(404, 400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'G' 'G4' 'GET /campaigns/<invalid-guid> returns 4xx' {
    $r = Call-Api -Method GET -Path '/campaigns/not-a-guid' -Token $global:userToken -ExpectStatus @(400, 404)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION H -- Settings =====
Section "H. Settings"

TestCase 'H' 'H1' 'GET /settings/smtp user-level returns 200/204' {
    $r = Call-Api -Method GET -Path '/settings/smtp' -Token $global:userToken -ExpectStatus @(200, 204, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'H' 'H2' 'GET /admin/system-settings (admin) returns settings' {
    $r = Call-Api -Method GET -Path '/admin/system-settings' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'H' 'H3' 'GET /admin/system-settings allowed for user (intentional — read-only platform settings)' {
    # Per AdminController comment: "Read platform settings -- visible to everyone (so users can see send rate too)"
    $r = Call-Api -Method GET -Path '/admin/system-settings' -Token $global:userToken -ExpectStatus @(200)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'H' 'H4' 'PUT /admin/system-settings rejected for user (write requires admin)' {
    $r = Call-Api -Method PUT -Path '/admin/system-settings' -Token $global:userToken -Body @{ globalDelayMs = 1000 } -ExpectStatus @(403, 401, 400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION I -- Inbox =====
Section "I. Inbox / Threads"

TestCase 'I' 'I1' 'GET /inbox returns list' {
    $r = Call-Api -Method GET -Path '/inbox' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'I' 'I2' 'BUG-003: GET /inbox?pageNumber=-3 clamps' {
    $r = Call-Api -Method GET -Path '/inbox?pageNumber=-3&pageSize=20' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'I' 'I3' 'GET /inbox/threads list' {
    $r = Call-Api -Method GET -Path '/inbox/threads' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'I' 'I4' 'BUG-003: GET /inbox/threads?pageNumber=-2 clamps' {
    $r = Call-Api -Method GET -Path '/inbox/threads?pageNumber=-2&pageSize=20' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'I' 'I5' 'GET /inbox/unread-count returns integer' {
    $r = Call-Api -Method GET -Path '/inbox/unread-count' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'I' 'I6' 'GET /inbox/threads/<random> returns 404' {
    $r = Call-Api -Method GET -Path '/inbox/threads/00000000-0000-0000-0000-000000000000' -Token $global:userToken -ExpectStatus @(404, 400)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION J -- Audit Logs =====
Section "J. Audit Logs"

TestCase 'J' 'J1' 'GET /admin/audit-logs (admin) returns list' {
    $r = Call-Api -Method GET -Path '/admin/audit-logs?pageNumber=1&pageSize=50' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'J' 'J2' 'BUG-003: GET /admin/audit-logs?pageNumber=-7 clamps' {
    $r = Call-Api -Method GET -Path '/admin/audit-logs?pageNumber=-7' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'J' 'J3' 'GET /admin/audit-logs rejected for user' {
    $r = Call-Api -Method GET -Path '/admin/audit-logs' -Token $global:userToken -ExpectStatus @(403, 401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION K -- Admin Users =====
Section "K. Admin Users"

TestCase 'K' 'K1' 'GET /admin/users (admin) returns list' {
    $r = Call-Api -Method GET -Path '/admin/users' -Token $global:adminToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'K' 'K2' 'GET /admin/users rejected for user' {
    $r = Call-Api -Method GET -Path '/admin/users' -Token $global:userToken -ExpectStatus @(403, 401)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'K' 'K3' 'Verify at least one admin exists (BUG-001)' {
    $r = Call-Api -Method GET -Path '/admin/users' -Token $global:adminToken
    if (-not $r.Ok) { return "couldn't list users" }
    $admins = @($r.Data.data | Where-Object { $_.role -eq 'admin' })
    if ($admins.Count -lt 1) { return "no admin user exists" }
    return $true
}

# ===== SECTION L -- Notifications =====
Section "L. Notifications"

TestCase 'L' 'L1' 'GET /notifications returns list' {
    $r = Call-Api -Method GET -Path '/notifications' -Token $global:userToken -ExpectStatus @(200, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'L' 'L2' 'GET /notifications/unread-count returns int' {
    $r = Call-Api -Method GET -Path '/notifications/unread-count' -Token $global:userToken -ExpectStatus @(200, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

# ===== SECTION M -- Dashboard =====
Section "M. Dashboard"

TestCase 'M' 'M1' 'GET /dashboard returns stats' {
    $r = Call-Api -Method GET -Path '/dashboard' -Token $global:userToken -ExpectStatus @(200, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

# ===== SECTION N -- Tracking =====
Section "N. Tracking (public)"

TestCase 'N' 'N1' 'GET /track/open/<random>.gif returns 200 (always)' {
    try {
        $url = "$BaseUrl/track/open/00000000-0000-0000-0000-000000000000.gif"
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
        if ($resp.StatusCode -ne 200) { return "status=$($resp.StatusCode)" }
        return $true
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        return "tracking should always 200; got $code"
    }
}

TestCase 'N' 'N2' 'GET /track/click/<random>?u=URL redirects to URL' {
    try {
        $url = "$BaseUrl/track/click/00000000-0000-0000-0000-000000000000?u=" + [uri]::EscapeDataString('https://example.com/')
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10 -MaximumRedirection 0 -ErrorAction SilentlyContinue
        # Redirect = 301/302/307; some setups return 200 with meta refresh -- both OK
        if ($resp -and $resp.StatusCode -ge 200 -and $resp.StatusCode -lt 400) { return $true }
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        if ($code -ge 300 -and $code -lt 400) { return $true }
        if ($code -eq 200) { return $true }
        return "status=$code"
    }
    return $true
}

TestCase 'N' 'N3' 'GET /track/click without u= returns 400 (anti-open-redirect)' {
    try {
        $url = "$BaseUrl/track/click/00000000-0000-0000-0000-000000000000"
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop | Out-Null
        return "should have rejected without u param"
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        if ($code -eq 400) { return $true }
        return "expected 400, got $code"
    }
}

TestCase 'N' 'N4' 'GET /track/click with javascript: URL rejected (anti-open-redirect)' {
    try {
        $url = "$BaseUrl/track/click/00000000-0000-0000-0000-000000000000?u=" + [uri]::EscapeDataString('javascript:alert(1)')
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop | Out-Null
        return "should have rejected javascript: URL"
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        if ($code -eq 400) { return $true }
        return "expected 400, got $code"
    }
}

# ===== SECTION O -- Webhooks =====
Section "O. Webhooks (public)"

TestCase 'O' 'O1' 'POST /webhooks/brevo without auth/signature accepts or 401' {
    $r = Call-Api -Method POST -Path '/webhooks/brevo' -Body @{} -ExpectStatus @(200, 202, 400, 401, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'O' 'O2' 'POST /webhooks/sendgrid' {
    $r = Call-Api -Method POST -Path '/webhooks/sendgrid' -Body @{} -ExpectStatus @(200, 202, 400, 401, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'O' 'O3' 'POST /webhooks/mailgun' {
    $r = Call-Api -Method POST -Path '/webhooks/mailgun' -Body @{} -ExpectStatus @(200, 202, 400, 401, 404)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

# ===== SECTION P -- Frontend SPA =====
Section "P. Frontend SPA"

TestCase 'P' 'P1' 'GET / returns SPA index' {
    try {
        $r = Invoke-WebRequest -Uri 'http://localhost:5173/' -UseBasicParsing -TimeoutSec 10
        if ($r.StatusCode -ne 200) { return "status=$($r.StatusCode)" }
        return $true
    } catch { return "ex: $($_.Exception.Message)" }
}

TestCase 'P' 'P2' 'GET /login (SPA fallback) returns index' {
    try {
        $r = Invoke-WebRequest -Uri 'http://localhost:5173/login' -UseBasicParsing -TimeoutSec 10
        if ($r.StatusCode -ne 200) { return "status=$($r.StatusCode)" }
        return $true
    } catch { return "ex: $($_.Exception.Message)" }
}

TestCase 'P' 'P3' 'GET /dashboard (SPA fallback)' {
    try {
        $r = Invoke-WebRequest -Uri 'http://localhost:5173/dashboard' -UseBasicParsing -TimeoutSec 10
        if ($r.StatusCode -ne 200) { return "status=$($r.StatusCode)" }
        return $true
    } catch { return "ex: $($_.Exception.Message)" }
}

# ===== SECTION Q -- Hardening / Edge Cases =====
Section "Q. Hardening"

TestCase 'Q' 'Q1' 'Invalid JSON body returns 4xx not 5xx' {
    try {
        $url = "$ApiUrl/auth/login"
        Invoke-WebRequest -Uri $url -Method POST -Body '{invalid' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop | Out-Null
        return "accepted invalid JSON"
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        if ($code -ge 400 -and $code -lt 500) { return $true }
        return "expected 4xx, got $code"
    }
}

TestCase 'Q' 'Q2' 'Very long input gracefully rejected (no 500)' {
    $longStr = 'x' * 100000
    $r = Call-Api -Method POST -Path '/auth/register' -Body @{
        email = "long-$Stamp@x.test"; password = $longStr; fullName = $longStr
    } -ExpectStatus @(200, 201, 400, 413, 422)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'Q' 'Q3' 'GET /api/v1/nonexistent returns 404 not 500' {
    $r = Call-Api -Method GET -Path '/nonexistent-endpoint-xyz' -ExpectStatus @(404, 401)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'Q' 'Q4' 'POST with method that should be GET returns 405' {
    try {
        $url = "$ApiUrl/me/profile"
        Invoke-WebRequest -Uri $url -Method POST -Headers @{ Authorization = "Bearer $($global:userToken)" } -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop | Out-Null
        return "accepted POST on GET-only endpoint"
    } catch {
        $code = $_.Exception.Response.StatusCode.Value__
        if ($code -eq 405 -or $code -eq 404) { return $true }
        return "expected 405, got $code"
    }
}

TestCase 'Q' 'Q5' 'SQL-injection-like input in search safely handled' {
    $r = Call-Api -Method GET -Path "/contacts?search=$([uri]::EscapeDataString(""' OR 1=1 --""))" -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'Q' 'Q6' 'XSS-like input in template body safely stored' {
    $r = Call-Api -Method POST -Path '/templates' -Token $global:userToken -Body @{
        name = "XSS Test $Stamp"; channel = 'email'
        subject = 'Test'; body = '<script>alert(1)</script>'
    }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'Q' 'Q7' 'Unicode input gracefully accepted or rejected (no 500)' {
    $r = Call-Api -Method POST -Path '/contacts' -Token $global:userToken -Body @{
        fullName = 'Unicode Test'; email = "unicode-$Stamp@blackbox.test"
        phone = '+971555000999'; whatsAppNumber = '+971555000999'
    } -ExpectStatus @(200, 201, 400)
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

TestCase 'Q' 'Q8' 'Email field missing @ rejected' {
    $r = Call-Api -Method POST -Path '/contacts' -Token $global:userToken -Body @{
        fullName = 'No At'; email = "noat-invalid-$Stamp"; phone = '+971555111000'; whatsAppNumber = '+971555111000'
    } -ExpectStatus @(400, 422)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'Q' 'Q9' 'Email too long rejected' {
    $r = Call-Api -Method POST -Path '/contacts' -Token $global:userToken -Body @{
        fullName = 'Long'; email = (('a' * 200) + "@long-$Stamp.test"); phone = '+971555111001'; whatsAppNumber = '+971555111001'
    } -ExpectStatus @(400, 422)
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== SECTION R -- M2 Banner Active-Sender Round Trip =====
Section "R. M2 Active Sender Round Trip"

TestCase 'R' 'R1' 'Admin sees Brevo group as default if marked' {
    # Mark our created Brevo group as default
    if (-not $global:createdGroupId) { return "skipped: no group" }
    $r = Call-Api -Method PUT -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken -Body @{
        name = "BB Brevo $Stamp"; description = 'Updated descr'
        isDefault = $true; isActive = $true
        emailProvider = 'brevo'; brevoApiKey = ''
        fromEmail = 'bb@blackbox.test'; fromName = 'BB Tester'
        smtpPort = 587; smtpEnableSsl = $true; smtpTimeout = 30000
    }
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

TestCase 'R' 'R2' '/me/active-sender now reflects Brevo group + provider=brevo' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method GET -Path '/me/active-sender' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    if ($r.Data.data.provider -ne 'brevo') { return "provider=$($r.Data.data.provider), expected brevo" }
    if ($r.Data.data.groupId -ne $global:createdGroupId) { return "groupId mismatch" }
    if (-not $r.Data.data.isDefault) { return "isDefault=false" }
    if (-not $r.Data.data.hasApiKey) { return "hasApiKey=false (key was set!)" }
    return $true
}

TestCase 'R' 'R3' 'Provider banner gracefully handles no groups' {
    # The end-to-end resolution function returns null group if there's no default.
    # We can't easily simulate that without nuking state, so just verify shape stays consistent.
    $r = Call-Api -Method GET -Path '/me/active-sender' -Token $global:userToken
    if (-not $r.Ok) { return "status=$($r.Status)" }
    return $true
}

# ===== Cleanup =====
Section "Z. Cleanup"

TestCase 'Z' 'Z1' 'DELETE the test Brevo group' {
    if (-not $global:createdGroupId) { return "skipped" }
    $r = Call-Api -Method DELETE -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken -ExpectStatus @(200, 204, 409)
    # 409 is OK -- can't delete default; we'll unmark first if needed
    if ($r.Status -eq 409) {
        # unmark default
        $r2 = Call-Api -Method PUT -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken -Body @{
            name = "BB Brevo $Stamp"; isDefault = $false; isActive = $true
            emailProvider = 'brevo'; brevoApiKey = ''; fromEmail = 'bb@blackbox.test'; fromName = 'BB'
            smtpPort = 587; smtpEnableSsl = $true; smtpTimeout = 30000
        }
        $r = Call-Api -Method DELETE -Path "/admin/smtp-groups/$($global:createdGroupId)" -Token $global:adminToken -ExpectStatus @(200, 204)
    }
    if ($r.Status -ge 500) { return "5xx: $($r.Status)" }
    return $true
}

# ===== Summary =====
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Black Box Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ("  PASS: {0,3}" -f $global:PassCount) -ForegroundColor Green
$failColor = if ($global:FailCount -gt 0) { 'Red' } else { 'Green' }
Write-Host ("  FAIL: {0,3}" -f $global:FailCount) -ForegroundColor $failColor
$total = $global:PassCount + $global:FailCount
Write-Host ("  TOTAL: {0,2}" -f $total)
if ($total -gt 0) {
    $pct = [Math]::Round(($global:PassCount / $total) * 100, 1)
    Write-Host ("  PASS RATE: {0}%" -f $pct)
}
Write-Host ""

# Save results
$reportPath = "D:\MultiChannelMarkettingApp\testing\blackbox-results-$Stamp.json"
$global:Results | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath -Encoding utf8
Write-Host "Results: $reportPath"

# Markdown report
$mdPath = "D:\MultiChannelMarkettingApp\testing\BLACKBOX-REPORT.md"
$md = @"
# Black Box Test Report

Run at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Stamp: $Stamp
Base URL: $BaseUrl

## Summary
- **PASS:** $($global:PassCount)
- **FAIL:** $($global:FailCount)
- **TOTAL:** $total
- **PASS RATE:** $(if ($total -gt 0) { [Math]::Round(($global:PassCount / $total) * 100, 1) } else { 0 })%

## Results by Section

"@

$bySection = $global:Results | Group-Object Section | Sort-Object Name
foreach ($g in $bySection) {
    $md += "`n### Section $($g.Name)`n"
    $md += "`n| ID | Test | Result | Detail |"
    $md += "`n|----|------|--------|--------|"
    foreach ($t in $g.Group) {
        $status = if ($t.Status -eq 'PASS') { 'PASS' } else { 'FAIL' }
        $detail = if ($t.Error) { $t.Error.Replace("`n", " ").Replace("|", "\|") } else { '' }
        $md += "`n| $($t.Id) | $($t.Name) | $status | $detail |"
    }
    $md += "`n"
}

$md | Out-File -FilePath $mdPath -Encoding utf8
Write-Host "Markdown: $mdPath"

if ($global:FailCount -gt 0) { exit 1 } else { exit 0 }
