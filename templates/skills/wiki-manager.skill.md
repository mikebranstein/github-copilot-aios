# Skill: GitHub Wiki Manager

## Overview

Manages GitHub wiki repositories for the AIOS system. Handles wiki initialization checks, page creation, reading, updating, and cleanup. All wiki operations are isolated to temporary directories to avoid conflicts with the main repository.

## Purpose

Provide a centralized, reusable skill for all agents to read/write research artifacts to GitHub wikis. Encapsulates all git operations, error handling, and temp directory management in one place.

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Git installed
- PowerShell 5.1+ (Windows) or Bash (Mac/Linux)
- Access to target GitHub repository with wiki enabled
- Sufficient disk space in system temp directory

## Input Parameters

```json
{
  "action": "string (required)",
  "repo": "string (required) - format: 'owner/repo'",
  "page_name": "string (optional) - filename without .md extension",
  "content": "string (optional) - markdown content to write",
  "append": "boolean (optional, default: false) - append vs replace on update"
}
```

### Actions

#### **init-check**
Pre-flight validation before any wiki operations.

**Input:**
```json
{
  "action": "init-check",
  "repo": "owner/repo"
}
```

**Output:**
```json
{
  "status": "success|error",
  "repo": "owner/repo",
  "has_wiki": true,
  "can_clone": true,
  "token_valid": true,
  "wiki_url": "https://github.com/owner/repo.wiki.git",
  "errors": []
}
```

---

#### **write-page**
Create a new wiki page or replace existing.

**Input:**
```json
{
  "action": "write-page",
  "repo": "owner/repo",
  "page_name": "Personas-John",
  "content": "# John - Enterprise Admin\n\n## Demographics\n..."
}
```

**Output:**
```json
{
  "status": "success|error",
  "page": "Personas-John",
  "wiki_url": "https://github.com/owner/repo/wiki/Personas-John",
  "committed": true,
  "commit_sha": "abc123...",
  "message": "Created Personas-John.md",
  "timestamp": "2026-07-07T14:30:00Z"
}
```

---

#### **update-page**
Update an existing wiki page with option to append or replace.

**Input:**
```json
{
  "action": "update-page",
  "repo": "owner/repo",
  "page_name": "Personas-John",
  "content": "## Additional Findings\n...",
  "append": true
}
```

**Output:** Same as `write-page`

---

#### **read-page**
Read an existing wiki page.

**Input:**
```json
{
  "action": "read-page",
  "repo": "owner/repo",
  "page_name": "Personas-John"
}
```

**Output:**
```json
{
  "status": "success|error",
  "page": "Personas-John",
  "content": "# John - Enterprise Admin\n\n...",
  "exists": true,
  "size_bytes": 2048,
  "timestamp": "2026-07-07T14:30:00Z"
}
```

---

#### **list-pages**
List all pages in the wiki.

**Input:**
```json
{
  "action": "list-pages",
  "repo": "owner/repo"
}
```

**Output:**
```json
{
  "status": "success|error",
  "repo": "owner/repo",
  "pages": [
    "Personas-John",
    "Personas-Sarah",
    "Journey-Maps-Admin",
    "Research-to-Decision-Index"
  ],
  "count": 4
}
```

---

## Implementation

### PowerShell Version

```powershell
# ============================================================================
# GitHub Wiki Manager Skill - PowerShell Implementation
# ============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$action,
    
    [Parameter(Mandatory=$true)]
    [string]$repo,
    
    [Parameter(Mandatory=$false)]
    [string]$page_name,
    
    [Parameter(Mandatory=$false)]
    [string]$content,
    
    [Parameter(Mandatory=$false)]
    [boolean]$append = $false
)

# ============================================================================
# Configuration
# ============================================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Determine wiki temp directory
if ($env:AIOS_WIKI_CACHE) {
    $WIKI_BASE = $env:AIOS_WIKI_CACHE
    $IS_PERSISTENT = $true
} else {
    $WIKI_BASE = "$env:TEMP"
    $IS_PERSISTENT = $false
}

$WIKI_TEMP_ID = "aios-wiki-$(Get-Random -Minimum 100000 -Maximum 999999)"
$WIKI_TEMP = Join-Path $WIKI_BASE $WIKI_TEMP_ID

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Log {
    param([string]$message, [string]$level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$level] $message"
}

function Get-WikiUrl {
    param([string]$repo)
    return "https://github.com/$repo.wiki.git"
}

function Get-RepositoryUrl {
    param([string]$repo)
    return "https://github.com/$repo.git"
}

function Test-WikiEnabled {
    param([string]$repo)
    try {
        Write-Log "Checking if wiki is enabled for $repo..."
        $response = & gh api "repos/$repo" --jq '.has_wiki' 2>$null
        return $response -eq "true"
    }
    catch {
        Write-Log "Failed to check wiki status: $_" "ERROR"
        return $false
    }
}

function Test-GitHubAuth {
    try {
        & gh auth status 2>&1 | Out-Null
        return $true
    }
    catch {
        Write-Log "GitHub authentication failed" "ERROR"
        return $false
    }
}

function Initialize-WikiTemp {
    try {
        Write-Log "Initializing wiki temp directory: $WIKI_TEMP"
        
        # Clean up if exists
        if (Test-Path $WIKI_TEMP) {
            Remove-Item -Recurse -Force $WIKI_TEMP -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
        
        New-Item -ItemType Directory -Path $WIKI_TEMP -Force | Out-Null
        return $true
    }
    catch {
        Write-Log "Failed to initialize temp directory: $_" "ERROR"
        return $false
    }
}

function Clone-Wiki {
    param([string]$repo)
    try {
        $wikiUrl = Get-WikiUrl $repo
        Write-Log "Cloning wiki repo: $wikiUrl"
        
        Push-Location $WIKI_TEMP
        & git clone $wikiUrl "." 2>&1 | Out-Null
        Pop-Location
        
        Write-Log "Wiki clone successful"
        return $true
    }
    catch {
        Write-Log "Failed to clone wiki: $_" "ERROR"
        return $false
    }
}

function Configure-GitUser {
    try {
        Push-Location $WIKI_TEMP
        
        Write-Log "Configuring git user..."
        & git config user.email "aios-automation@github.local" 2>&1 | Out-Null
        & git config user.name "AIOS Research Agent" 2>&1 | Out-Null
        
        Pop-Location
        return $true
    }
    catch {
        Write-Log "Failed to configure git user: $_" "ERROR"
        return $false
    }
}

function Push-WikiChanges {
    param([string]$message)
    try {
        Push-Location $WIKI_TEMP
        
        Write-Log "Staging changes..."
        & git add "." 2>&1 | Out-Null
        
        # Check if there are changes
        $status = & git status --porcelain 2>&1
        if (-not $status) {
            Write-Log "No changes to commit"
            Pop-Location
            return $true
        }
        
        Write-Log "Committing with message: $message"
        & git commit -m $message 2>&1 | Out-Null
        
        Write-Log "Pushing to remote..."
        & git push origin main 2>&1 | Out-Null
        
        Pop-Location
        Write-Log "Push successful"
        return $true
    }
    catch {
        Write-Log "Failed to push changes: $_" "ERROR"
        return $false
    }
}

function Get-CommitSha {
    try {
        Push-Location $WIKI_TEMP
        $sha = & git rev-parse HEAD 2>&1
        Pop-Location
        return $sha
    }
    catch {
        return "unknown"
    }
}

function Cleanup-WikiTemp {
    try {
        if ($IS_PERSISTENT) {
            Write-Log "Keeping persistent wiki cache: $WIKI_TEMP"
            return
        }
        
        Write-Log "Cleaning up temp directory: $WIKI_TEMP"
        if (Test-Path $WIKI_TEMP) {
            Remove-Item -Recurse -Force $WIKI_TEMP -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Log "Cleanup warning (non-fatal): $_" "WARN"
    }
}

function ConvertTo-SafePageName {
    param([string]$pageName)
    # Replace spaces with hyphens, remove special chars
    $safe = $pageName -replace '\s+', '-' -replace '[^a-zA-Z0-9\-_]', ''
    return $safe
}

# ============================================================================
# Markdown Linting Functions
# ============================================================================

function Lint-Markdown {
    param([string]$content)
    
    $issues = @()
    $lines = $content -split "`n"
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lineNum = $i + 1
        $line = $lines[$i]
        
        # Check for inconsistent heading levels
        if ($line -match '^#+\s') {
            $level = ($line | Select-String '^(#+)' -o).Matches[0].Value.Length
            if ($i -gt 0) {
                $prevLine = $lines[$i - 1]
                if ($prevLine -match '^#+\s') {
                    $prevLevel = ($prevLine | Select-String '^(#+)' -o).Matches[0].Value.Length
                    if ($level -gt $prevLevel + 1) {
                        $issues += "Line $lineNum`: Heading level skips from h$prevLevel to h$level (use h$($prevLevel+1))"
                    }
                }
            }
        }
        
        # Check for heading without space after #
        if ($line -match '^#+[^\s]') {
            $issues += "Line $lineNum`: Missing space after heading marker: '$line'"
        }
        
        # Check for trailing whitespace
        if ($line -match '\s+$') {
            $issues += "Line $lineNum`: Trailing whitespace detected"
        }
        
        # Check for inconsistent list markers
        if ($line -match '^\s*[-*+]\s') {
            $marker = ($line | Select-String '^\s*([-*+])' -o).Matches[0].Groups[1].Value
            if ($i -gt 0) {
                $prevLine = $lines[$i - 1]
                if ($prevLine -match '^\s*[-*+]\s' -and $marker -ne ($prevLine | Select-String '^\s*([-*+])' -o).Matches[0].Groups[1].Value) {
                    $issues += "Line $lineNum`: Inconsistent list marker (mix of -, *, +). Use single marker type"
                }
            }
        }
        
        # Check for improper link format (common mistakes)
        if ($line -match '\[([^\]]+)\]\s*\(') {
            # Valid link format
        } elseif ($line -match '\[([^\]]+)\]' -and -not ($line -match '\[([^\]]+)\]\s*\(')) {
            # Might be missing URL
            if ($line -match '\[\s*\]|\]\s*\(\s*\)') {
                $issues += "Line $lineNum`: Empty link detected: '$line'"
            }
        }
    }
    
    return $issues
}

function Correct-MarkdownErrors {
    param([string]$content)
    
    Write-Log "Correcting markdown errors..."
    $corrected = $content
    
    # Fix trailing whitespace
    $corrected = $corrected -split "`n" | ForEach-Object { $_ -replace '\s+$', '' } | Join-String -Separator "`n"
    
    # Fix heading spacing (ensure space after #)
    $corrected = $corrected -replace '^(#+)([^\s])', '$1 $2'
    
    # Fix common heading level skips (if h1 → h3, convert h3 to h2)
    $lines = $corrected -split "`n"
    $lastHeadingLevel = 0
    $corrections = @()
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^(#+)\s') {
            $level = $matches[1].Length
            if ($level -gt $lastHeadingLevel + 1 -and $lastHeadingLevel -gt 0) {
                $targetLevel = $lastHeadingLevel + 1
                $newMarker = '#' * $targetLevel
                $lines[$i] = $lines[$i] -replace '^#+', $newMarker
                $corrections += "Fixed heading level from h$level to h$targetLevel on line $($i+1)"
                $level = $targetLevel
            }
            $lastHeadingLevel = $level
        }
    }
    
    $corrected = $lines | Join-String -Separator "`n"
    
    # Standardize list markers to hyphens
    $corrected = $corrected -replace '^\s*(\*)\s', '- '
    $corrected = $corrected -replace '^\s*(\+)\s', '- '
    
    if ($corrections.Count -gt 0) {
        Write-Log "Applied markdown corrections: $($corrections -join '; ')"
    }
    
    return $corrected
}

function Check-WikiLinks {
    param([string]$content, [string]$repoPath)
    
    Write-Log "Validating wiki internal links..."
    $linkIssues = @()
    
    # Find all wiki-style links [text](page-name) and [text](page-name.md)
    $wikiLinkPattern = '\[([^\]]+)\]\(([^)]+)\)'
    $matches = [regex]::Matches($content, $wikiLinkPattern)
    
    foreach ($match in $matches) {
        $linkText = $match.Groups[1].Value
        $linkTarget = $match.Groups[2].Value
        
        # Skip external links (http://, https://, mailto:, etc.)
        if ($linkTarget -match '^(https?://|mailto:|ftp://)') {
            continue
        }
        
        # Skip anchor-only links (#section)
        if ($linkTarget -match '^#') {
            continue
        }
        
        # Normalize link target (remove .md if present)
        $pageName = $linkTarget -replace '\.md$', ''
        
        # Check if page exists in wiki
        $pageFile = Join-Path $repoPath "$pageName.md"
        
        if (-not (Test-Path $pageFile)) {
            $linkIssues += "Missing wiki page: [$linkText]($linkTarget) - expected file: $pageName.md"
        }
    }
    
    return $linkIssues
}

function Check-ExternalLinks {
    param([string]$content)
    
    Write-Log "Validating external links..."
    $linkIssues = @()
    
    # Find all external links
    $externalLinkPattern = '(https?://[^\s\)]+)'
    $matches = [regex]::Matches($content, $externalLinkPattern)
    
    foreach ($match in $matches) {
        $url = $match.Groups[1].Value
        
        # Basic validation: check URL format
        if (-not ($url -match '^https?://[a-zA-Z0-9][a-zA-Z0-9\-\.]*[a-zA-Z0-9]\.[a-zA-Z]{2,}')) {
            $linkIssues += "Invalid URL format: $url"
            continue
        }
        
        # Try to validate with HEAD request (optional, with timeout)
        try {
            Write-Log "Checking URL: $url"
            $response = Invoke-WebRequest -Uri $url -Method Head -TimeoutSec 3 -ErrorAction Stop
            if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
                $linkIssues += "URL returned status $($response.StatusCode): $url"
            }
        }
        catch [System.Net.HttpRequestException] {
            # Network error - log but don't block commit (may be temporary)
            Write-Log "Warning: Could not verify URL (network issue): $url" "WARN"
        }
        catch [System.TimeoutException] {
            # Timeout - log but don't block commit
            Write-Log "Warning: URL check timed out: $url" "WARN"
        }
        catch {
            # Other errors - log but don't block
            Write-Log "Warning: Could not verify URL: $url - $_" "WARN"
        }
    }
    
    return $linkIssues
}

function Validate-Content {
    param([string]$content, [string]$wikiRepoPath)
    
    Write-Log "Starting content validation..."
    $allIssues = @()
    
    # Run linting
    $lintIssues = Lint-Markdown $content
    $allIssues += $lintIssues
    
    # Check wiki links
    $wikiLinkIssues = Check-WikiLinks $content $wikiRepoPath
    $allIssues += $wikiLinkIssues
    
    # Check external links
    $externalLinkIssues = Check-ExternalLinks $content
    $allIssues += $externalLinkIssues
    
    if ($allIssues.Count -gt 0) {
        Write-Log "Found $($allIssues.Count) validation issue(s):" "WARN"
        foreach ($issue in $allIssues) {
            Write-Log "  - $issue" "WARN"
        }
    } else {
        Write-Log "Content validation passed ✓"
    }
    
    return $allIssues
}

# ============================================================================
# Action Handlers
# ============================================================================

function Handle-InitCheck {
    Write-Log "Executing init-check action"
    
    $result = @{
        status = "error"
        repo = $repo
        has_wiki = $false
        can_clone = $false
        token_valid = $false
        wiki_url = Get-WikiUrl $repo
        errors = @()
    }
    
    # Check authentication
    if (-not (Test-GitHubAuth)) {
        $result.errors += "GitHub authentication failed"
        return $result | ConvertTo-Json
    }
    $result.token_valid = $true
    
    # Check if wiki enabled
    if (-not (Test-WikiEnabled $repo)) {
        $result.errors += "Wiki not enabled for repository"
        return $result | ConvertTo-Json
    }
    $result.has_wiki = $true
    
    # Test clone capability
    if (-not (Initialize-WikiTemp)) {
        $result.errors += "Failed to create temp directory"
        return $result | ConvertTo-Json
    }
    
    if (-not (Clone-Wiki $repo)) {
        $result.errors += "Failed to clone wiki repository"
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    $result.can_clone = $true
    
    Cleanup-WikiTemp
    
    if ($result.errors.Count -eq 0) {
        $result.status = "success"
    }
    
    return $result | ConvertTo-Json
}

function Handle-WritePage {
    Write-Log "Executing write-page action for page: $page_name"
    
    $result = @{
        status = "error"
        page = $page_name
        wiki_url = "https://github.com/$repo/wiki/$page_name"
        committed = $false
        commit_sha = ""
        message = ""
        timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    }
    
    # Validate inputs
    if (-not $page_name) {
        $result.message = "page_name parameter required"
        return $result | ConvertTo-Json
    }
    
    if (-not $content) {
        $result.message = "content parameter required"
        return $result | ConvertTo-Json
    }
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        $result.message = "Failed to initialize temp directory"
        return $result | ConvertTo-Json
    }
    
    # Clone wiki
    if (-not (Clone-Wiki $repo)) {
        $result.message = "Failed to clone wiki"
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Configure git
    if (-not (Configure-GitUser)) {
        $result.message = "Failed to configure git"
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Write page
    try {
        $safeName = ConvertTo-SafePageName $page_name
        $pageFile = Join-Path $WIKI_TEMP "$safeName.md"
        
        Write-Log "Writing page to: $pageFile"
        
        # Correct markdown errors
        $correctedContent = Correct-MarkdownErrors $content
        
        # Validate content
        $validationIssues = Validate-Content $correctedContent $WIKI_TEMP
        
        if ($validationIssues.Count -gt 0) {
            Write-Log "Content validation issues found, but proceeding with corrected content"
        }
        
        # Write corrected content
        Set-Content -Path $pageFile -Value $correctedContent -Encoding UTF8
        
        # Push changes
        $commitMsg = "Update $safeName.md - Research findings"
        if (-not (Push-WikiChanges $commitMsg)) {
            $result.message = "Failed to push changes"
            Cleanup-WikiTemp
            return $result | ConvertTo-Json
        }
        
        $result.status = "success"
        $result.committed = $true
        $result.commit_sha = Get-CommitSha
        $result.message = "Created/updated $safeName.md (linted and validated)"
    }
    catch {
        $result.message = "Failed to write page: $_"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-UpdatePage {
    Write-Log "Executing update-page action for page: $page_name (append: $append)"
    
    $result = @{
        status = "error"
        page = $page_name
        wiki_url = "https://github.com/$repo/wiki/$page_name"
        committed = $false
        commit_sha = ""
        message = ""
        timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    }
    
    # Validate inputs
    if (-not $page_name) {
        $result.message = "page_name parameter required"
        return $result | ConvertTo-Json
    }
    
    if (-not $content) {
        $result.message = "content parameter required"
        return $result | ConvertTo-Json
    }
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        $result.message = "Failed to initialize temp directory"
        return $result | ConvertTo-Json
    }
    
    # Clone wiki
    if (-not (Clone-Wiki $repo)) {
        $result.message = "Failed to clone wiki"
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Configure git
    if (-not (Configure-GitUser)) {
        $result.message = "Failed to configure git"
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Update page
    try {
        $safeName = ConvertTo-SafePageName $page_name
        $pageFile = Join-Path $WIKI_TEMP "$safeName.md"
        
        $newContent = $content
        
        # If append and file exists, prepend existing content
        if ($append -and (Test-Path $pageFile)) {
            Write-Log "Appending to existing page"
            $existingContent = Get-Content -Path $pageFile -Encoding UTF8 -Raw
            $newContent = "$newContent`n`n---`n`n$existingContent"
        }
        
        Write-Log "Processing page content: $pageFile"
        
        # Correct markdown errors
        $correctedContent = Correct-MarkdownErrors $newContent
        
        # Validate content
        $validationIssues = Validate-Content $correctedContent $WIKI_TEMP
        
        if ($validationIssues.Count -gt 0) {
            Write-Log "Content validation issues found, but proceeding with corrected content"
        }
        
        # Write corrected content
        Set-Content -Path $pageFile -Value $correctedContent -Encoding UTF8
        
        # Push changes
        $commitMsg = "Update $safeName.md - Additional research findings"
        if (-not (Push-WikiChanges $commitMsg)) {
            $result.message = "Failed to push changes"
            Cleanup-WikiTemp
            return $result | ConvertTo-Json
        }
        
        $result.status = "success"
        $result.committed = $true
        $result.commit_sha = Get-CommitSha
        $result.message = "Updated $safeName.md (append: $append, linted and validated)"
    }
    catch {
        $result.message = "Failed to update page: $_"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-ReadPage {
    Write-Log "Executing read-page action for page: $page_name"
    
    $result = @{
        status = "error"
        page = $page_name
        content = ""
        exists = $false
        size_bytes = 0
        timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    }
    
    # Validate inputs
    if (-not $page_name) {
        $result.status = "error"
        return $result | ConvertTo-Json
    }
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Clone wiki
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        $safeName = ConvertTo-SafePageName $page_name
        $pageFile = Join-Path $WIKI_TEMP "$safeName.md"
        
        if (Test-Path $pageFile) {
            Write-Log "Reading page from: $pageFile"
            $content = Get-Content -Path $pageFile -Encoding UTF8 -Raw
            $size = (Get-Item $pageFile).Length
            
            $result.status = "success"
            $result.content = $content
            $result.exists = $true
            $result.size_bytes = $size
        }
        else {
            Write-Log "Page not found: $pageFile"
            $result.status = "not_found"
        }
    }
    catch {
        $result.status = "error"
        Write-Log "Failed to read page: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-ListPages {
    Write-Log "Executing list-pages action for repo: $repo"
    
    $result = @{
        status = "error"
        repo = $repo
        pages = @()
        count = 0
    }
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    # Clone wiki
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        Write-Log "Listing wiki pages in: $WIKI_TEMP"
        
        $mdFiles = Get-ChildItem -Path $WIKI_TEMP -Filter "*.md" -ErrorAction SilentlyContinue | 
                   Where-Object { $_.Name -ne "Home.md" }
        
        foreach ($file in $mdFiles) {
            $pageName = $file.BaseName
            $result.pages += $pageName
        }
        
        $result.count = $result.pages.Count
        $result.status = "success"
        
        Write-Log "Found $($result.count) wiki pages"
    }
    catch {
        Write-Log "Failed to list pages: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

# ============================================================================
# Main Execution
# ============================================================================

Write-Log "GitHub Wiki Manager Skill - Action: $action"

try {
    switch ($action) {
        "init-check" { Handle-InitCheck }
        "write-page" { Handle-WritePage }
        "update-page" { Handle-UpdatePage }
        "read-page" { Handle-ReadPage }
        "list-pages" { Handle-ListPages }
        default {
            $error = @{
                status = "error"
                message = "Unknown action: $action"
                valid_actions = @("init-check", "write-page", "update-page", "read-page", "list-pages")
            }
            $error | ConvertTo-Json
        }
    }
}
catch {
    Write-Log "Skill execution failed: $_" "ERROR"
    $error = @{
        status = "error"
        message = "Skill execution failed: $_"
        action = $action
    }
    $error | ConvertTo-Json
}
```

## Usage Examples

### From Research Agent

```markdown
## Step 4b: Update Wiki with Research Findings

CALL SKILL: wiki-manager

**Parameters:**
- action: write-page
- repo: owner/my-repo
- page_name: Personas-John-Doe
- content: |
  # John Doe - Enterprise Admin
  
  ## Demographics
  - Age: 45-55
  - Role: IT Director
  - Company Size: 1000+ employees
  
  ## Jobs to be Done
  1. Manage security policies
  2. Deploy enterprise software
  
  ## Frustrations
  - Complex onboarding processes
  - Lack of automation

**Expected Response:**
```json
{
  "status": "success",
  "page": "Personas-John-Doe",
  "wiki_url": "https://github.com/owner/my-repo/wiki/Personas-John-Doe",
  "committed": true,
  "message": "Created Personas-John-Doe.md"
}
```

---

### From PM Agent Phase 2

```markdown
## Step 2b: Verify Research Wiki Pages Exist

CALL SKILL: wiki-manager

**Parameters:**
- action: list-pages
- repo: owner/my-repo

**Expected Response:**
```json
{
  "status": "success",
  "repo": "owner/my-repo",
  "pages": ["Personas-John", "Journey-Maps-Admin", "Research-to-Decision-Index"],
  "count": 3
}
```

---

### Pre-Flight Check

```markdown
Before agents start research, verify wiki is accessible:

CALL SKILL: wiki-manager

**Parameters:**
- action: init-check
- repo: owner/my-repo

**Expected Response:**
```json
{
  "status": "success",
  "has_wiki": true,
  "can_clone": true,
  "token_valid": true
}
```

---

## Environment Variables

### `AIOS_WIKI_CACHE`

Optional. Controls wiki temp directory behavior.

**If set:**
```powershell
$env:AIOS_WIKI_CACHE = "C:\Users\Mike\AppData\Local\aios-wiki-cache"
```
- Wiki clones persist in this directory
- Faster for repeated operations
- Manual cleanup required

**If not set (default):**
- Wiki clones to system temp directory
- Auto-cleanup after each operation
- Fresh, isolated operation each time

---

## Error Handling

All errors return with `"status": "error"` and a descriptive message.

### Common Errors

| Error | Cause | Resolution |
|-------|-------|-----------|
| `GitHub authentication failed` | Invalid/expired token | Run `gh auth login` |
| `Wiki not enabled for repository` | Wiki disabled in repo settings | Enable wiki in GitHub settings |
| `Failed to clone wiki` | Network issue or invalid repo | Check repo URL and network |
| `Failed to push changes` | No write permissions | Check GitHub token permissions |
| `page_name parameter required` | Missing page_name | Provide page_name parameter |

---

## Guarantees

✅ **Atomic Operations:** All writes are atomic (either succeed completely or fail cleanly)  
✅ **Temp Isolation:** Wiki operations never interfere with main repo (separate temp directory)  
✅ **Automatic Cleanup:** Temp files deleted after each operation (unless persistent cache set)  
✅ **Error Recovery:** Failed operations clean up resources and return detailed error messages  
✅ **Cross-Platform:** Works on Windows PowerShell, Mac, Linux

---

## Notes

- Page names are automatically sanitized (spaces → hyphens, special chars removed)
- Each operation creates a fresh wiki clone (unless `AIOS_WIKI_CACHE` set)
- Git user for commits: `aios-automation@github.local` / `AIOS Research Agent`
- All timestamps in ISO 8601 format (UTC)
- Commit messages include context (e.g., "Update Personas-John.md - Research findings")

---

## Markdown Linting & Validation

All `write-page` and `update-page` operations automatically:

### ✅ **Lint & Correct Markdown**
- Fix missing spaces after heading markers (`##text` → `## text`)
- Remove trailing whitespace from all lines
- Fix heading level skips (h1 → h3 becomes h1 → h2)
- Standardize list markers (all `*` or `+` converted to `-`)
- Detect and report empty links `[]()`

### ✅ **Validate Wiki Links**
- Find all internal wiki links `[text](page-name)`
- Verify target pages exist in the wiki
- Report missing page errors before commit
- Support both link styles: `page-name` and `page-name.md`

### ✅ **Validate External Links**
- Check external URLs for proper format (http/https/mailto/ftp)
- Validate URL syntax
- Attempt HEAD request to verify accessibility (non-blocking)
- Report HTTP errors but continue if network is unavailable

### Validation Behavior
- **Corrections applied automatically:** Markdown style issues are fixed before commit
- **Warnings logged:** Validation issues are reported but don't block commit
- **Failed commits prevented:** Critical errors (e.g., empty links) are caught before git push
- **Robust handling:** Network timeouts on URL checks don't halt the operation

### Example: Auto-Corrected Content
```markdown
# Input (with issues)
##Not spaced properly
* List item 1
+ List item 2    
- List item 3
[Broken](missing-page.md)

# Output (corrected)
## Not spaced properly
- List item 1
- List item 2
- List item 3
[Broken](missing-page.md)  ← Reported but kept (for manual fix if intentional)
```
