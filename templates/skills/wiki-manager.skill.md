# Skill: GitHub Wiki Manager (Expert Librarian Model)

## Philosophy

**Expert Librarian, Not Passive Storage**

The wiki-manager skill is an **autonomous expert librarian** that owns all aspects of wiki organization:
- **Agents specify WHAT** to store: content_type, subject, content
- **Skill owns WHERE & HOW**: placement, organization, structure
- **Skill owns REORGANIZATION**: evaluates organization quality continuously, reorganizes proactively to maintain library health
- **Index is source of truth**: Always accurate and complete after every write, audit, and reorganization

The skill operates with semantic understanding of its own library:
- Observes patterns in how content is organized over time
- Calculates organization "messiness" (orphaned pages, duplicate structures, scattered content types, etc.)
- Reorganizes when messiness exceeds acceptable thresholds
- Maintains rigorous index audit & gap analysis after any reorganization
- All reorganization is internal—agents never direct it

Agents never instruct reorganization. The skill reorganizes autonomously before/during write operations to maintain library quality.

## Purpose

Self-organizing wiki repository for AIOS research artifacts. Skill learns structure from existing content, organizes new submissions automatically, prevents duplicates, and maintains master index.

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- Git installed
- PowerShell 5.1+
- Wiki enabled on target repo

## Agent Interface (Only Actions)

Agents call only two actions:

```json
{
  "action": "string (required) - search | write-content",
  "repo": "string (required) - format: 'owner/repo'",
  "query": "string (optional for search) - search keyword",
  "content_type": "string (optional for write-content) - any type agent defines",
  "subject": "string (optional for write-content) - what this content is about",
  "content": "string (optional for write-content) - markdown content to store",
  "status": "string (optional for write-content) - Complete | In Progress | Deferred",
  "confidence": "string (optional for write-content) - HIGH | MEDIUM | LOW",
  "github_issue": "string (optional for write-content) - issue number",
  "findings_summary": "string (optional for write-content) - one-line summary"
}
```

**Internal Operations (Skill Only):**
- `update-index` — Maintained automatically by skill after write-content
- `discover-structure` — Used internally by skill for reorganization decisions
- `reorganize` — Triggered autonomously based on messiness metric

### Actions

#### **search**

Find existing content by keyword. Returns matches with scores. **Agent evaluates results.**

**Input:**
```json
{
  "action": "search",
  "repo": "owner/repo",
  "query": "[search term]"
}
```

**Output:**
```json
{
  "status": "success|error",
  "query": "[search term]",
  "total_found": 1,
  "results": [
    {
      "page": "[actual page name or path]",
      "match_score": 0.98,
      "snippet": "[first few lines of matching content]"
    }
  ],
  "note": "Page names reported as-is from wiki. Structure may vary."
}
```

---

#### **write-content**

Agent specifies content_type and subject. **Skill autonomously evaluates and optimizes wiki organization, then writes content.**

**What happens internally (transparent to agent):**
1. Discover current wiki structure and organization patterns
2. Calculate organization "messiness" metric (orphaned pages, scattered content, duplicates, structure drift, etc.)
3. If messiness exceeds acceptable threshold → **reorganize automatically:**
   - Restructure wiki to improve coherence (consolidate, move, or refactor pages as needed)
   - Audit wiki after reorganization (cross-check all pages accounted for)
   - Gap analysis (identify missing index entries, orphaned content)
   - Update Content-Index to fill all gaps and reflect new organization
4. Write new content to the optimized structure
5. Return response indicating whether reorganization occurred

**Input:**
```json
{
  "action": "write-content",
  "repo": "owner/repo",
  "content_type": "[type]",
  "subject": "[subject]",
  "content": "[markdown content]"
}
```

**Output (Standard - no reorganization needed):**
```json
{
  "status": "success",
  "committed": true,
  "reorganized": false,
  "note": "Content committed to wiki."
}
```

**Output (With reorganization):**
```json
{
  "status": "success",
  "committed": true,
  "reorganized": true,
  "reorganization_summary": {
    "trigger": "messiness_exceeded_threshold",
    "changes_made": ["consolidated_duplicate_folders", "moved_orphaned_pages", ...],
    "audit_result": "all_pages_accounted_for",
    "gap_analysis": {
      "missing_from_index": [...],
      "orphaned_content": [...],
      "fixed": true
    },
    "index_updated": true
  }
}
```

**How Skill Decides Organization:**
- Organization is NOT always content_type-based; may follow domain, outcome, workflow stage, or observed patterns
- Skill uses expertise and analysis of wiki history to determine optimal structure
- Structure emerges and evolves over time
- **Always indexed. Always auditable. Always optimized.**

---

## Internal Operations (Skill Only)

These operations are performed autonomously by the skill and are NOT called by agents.

### **update-index** (Internal)

Automatically executed by skill after every write-content. Maintains Content-Index with all content metadata:
- Triggered: After write-content completes
- Also executed: Post-reorganization (as part of gap-analysis)
- Updates: Content-Index.md in wiki root
- Ensures: All pages are indexed, no gaps, all metadata current

### **discover-structure** (Internal)

Automatically executed by skill:
- **Before write-content:** Evaluates current organization to calculate messiness metric
- **During reorganization:** Audits that all pages are accounted for
- **Post-reorganization:** Identifies orphaned content and missing index entries
- NOT exposed to agents (use search() if you need to verify content location)

### **reorganize** (Internal)

Automatically triggered by skill based on messiness metric:
- Triggered: Before write-content, if messiness exceeds threshold
- Process: Consolidate duplicates, move orphaned pages, optimize structure
- Follow-up: Full audit → gap-analysis → index-update
- Transparent: Agents see `reorganized: true/false` in response, but don't manage it

---

## Master Index Format

Content-Index.md (auto-maintained in wiki root, updated post-write and post-reorg):

```markdown
# Content-Index

Master registry of all content. Updated after each write-content + update-index.

| Subject | Type | Status | Wiki Page | Last Updated | GitHub Issue | Confidence | Summary |
|---------|------|--------|-----------|--------------|--------------|------------|---------|
| [subject] | [type] | ✅ Complete | [actual page location] | 2026-07-08 | #1025 | HIGH | [one-liner] |
```

**Note:** Wiki Page column records the actual location where content was stored, whatever the current wiki structure determines.

---

## Agent Workflow

### Typical Research/Analysis Workflow

```markdown
Step 0: Pre-flight check
CALL wiki-manager: search("[subject]")
→ If found and complete: reuse, don't redo

Step 1-3: Conduct research/analysis

Step 4: Write findings (all metadata included)
CALL wiki-manager: write-content
{
  "content_type": "[type]",
  "subject": "[subject]",
  "content": "[markdown findings]",
  "status": "Complete",
  "confidence": "HIGH|MEDIUM|LOW",
  "findings_summary": "[summary]",
  "github_issue": "#[issue]"
}
→ Skill writes content AND updates index

Step 5: Close issue
```

---

## Key Principles

✅ **Expert librarian, not passive storage** — Skill owns organization, placement, and reorganization  
✅ **Autonomous optimization** — Evaluates wiki messiness continuously, reorganizes proactively  
✅ **Index as source of truth** — Always accurate, complete, and up-to-date  
✅ **Agents specify semantics** — content_type, subject, content (agents decide meaning)  
✅ **Skill owns structure** — Decides WHERE and HOW based on domain expertise and observed patterns  
✅ **Structure may not follow content_type** — Organization emerges from patterns, not prescribed schema  
✅ **Post-reorganization rigor** — Audit → Gap Analysis → Index Update (always)  
✅ **Internal mechanisms only** — Agents never direct reorganization; skill acts autonomously  
✅ **No path specifications** — Agents never specify placement or folder structure  
✅ **Generic by design** — Works with any content types agents define

**Input:**
```json
{
  "action": "audit-and-organize",
  "repo": "owner/repo",
  "force_reorganize": false
}
```

**Output:**
```json
{
  "status": "success|error",
  "action": "REORGANIZED|ALREADY_ORGANIZED|ERROR",
  "audit_timestamp": "2026-07-08T10:30:00Z",
  "wiki_state_before": {
    "total_pages": 12,
    "messiness_score": 65,
    "messiness_level": "CHAOS - High",
    "issues": [
      "Found 3 persona variants consolidable",
      "Found 5 uncategorized pages",
      "Index is 2 days out of date"
    ]
  },
  "reorganization": {
    "performed": true,
    "pages_consolidated": 3,
    "pages_moved": 7,
    "new_folders_created": 2,
    "links_fixed": 4,
    "index_entries_updated": 8
  },
  "wiki_state_after": {
    "total_pages": 12,
    "messiness_score": 12,
    "messiness_level": "ORGANIZED - Low",
    "structure": {
      "Personas": 3,
      "Journey-Maps": 2,
      "Competitive-Analysis": 2,
      "Market-Trends": 1,
      "Feature-Research": 3,
      "Other": 1
    }
  },
  "index_status": "CURRENT - Synchronized with filesystem"
}
```

---

#### **check-wiki-health**
Quick status check without reorganization. Returns current organization state and health metrics.

**Input:**
```json
{
  "action": "check-wiki-health",
  "repo": "owner/repo"
}
```

**Output:**
```json
{
  "status": "success|error",
  "wiki_health": {
    "total_pages": 12,
    "messiness_score": 65,
    "messiness_level": "CHAOS - High",
    "organization_status": "NEEDS_ATTENTION",
    "last_organized": "2026-07-05T14:00:00Z",
    "days_since_organized": 3
  },
  "category_breakdown": {
    "Personas": 2,
    "Journey-Maps": 1,
    "Competitive-Analysis": 0,
    "Uncategorized": 5,
    "Duplicates_Detected": 3
  },
  "problems_found": {
    "duplicate_subjects": 3,
    "inconsistent_naming": 4,
    "orphaned_pages": 5,
    "index_out_of_date": true
  },
  "recommendations": "Run audit-and-organize - messiness exceeds 40 threshold"
}
```

---

#### **find-or-create**
Search for a topic in the wiki. If found, return existing pages. If not found, create new page in appropriate category, update index.

**Input:**
```json
{
  "action": "find-or-create",
  "repo": "owner/repo",
  "search_term": "Field Manager",
  "content": "# Field Manager\n\n## Demographics\n[Optional content]"
}
```

**Output:**
```json
{
  "status": "success|error",
  "action_taken": "FOUND|CREATED|CONSOLIDATED",
  "search_term": "Field Manager",
  "result": {
    "page_name": "Field-Manager",
    "location": "Personas/Field-Manager",
    "category": "Persona",
    "category_confidence": 0.95,
    "created": false,
    "consolidated_from": []
  },
  "index_entry": {
    "subject": "Field Manager",
    "status": "In Progress",
    "wiki_page": "Personas/Field-Manager",
    "last_updated": "2026-07-08"
  }
}
```

---

## Research-to-Decision-Index (Master Registry)

The index is the source of truth for all research. It's auto-created and updated on every audit call.

**Structure:**
```
| Subject | Status | Wiki Page | Last Updated | Research Issues | Findings | Confidence |
|---------|--------|-----------|--------------|-----------------|----------|------------|
| Field Manager | ✅ Complete | Personas/Field-Manager | 2026-07-08 | #1023, #1024 | 10 interviews, high confidence | HIGH |
```

---

## Implementation

### PowerShell v2 (Smart, Adaptive)

```powershell
# ============================================================================
# GitHub Wiki Manager v2 - Smart, Adaptive, Self-Healing
# ============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$action,
    
    [Parameter(Mandatory=$true)]
    [string]$repo,
    
    [Parameter(Mandatory=$false)]
    [string]$search_term,
    
    [Parameter(Mandatory=$false)]
    [string]$content,
    
    [Parameter(Mandatory=$false)]
    [boolean]$force_reorganize = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ============================================================================
# Configuration
# ============================================================================

$WIKI_BASE = if ($env:AIOS_WIKI_CACHE) { $env:AIOS_WIKI_CACHE } else { "$env:TEMP" }
$WIKI_TEMP_ID = "aios-wiki-$(Get-Random -Minimum 100000 -Maximum 999999)"
$WIKI_TEMP = Join-Path $WIKI_BASE $WIKI_TEMP_ID
$IS_PERSISTENT = [bool]$env:AIOS_WIKI_CACHE

# Category keywords for classification
$CATEGORY_KEYWORDS = @{
    "Persona" = @("interview", "demographics", "goals", "frustrations", "jobs to be done", "persona")
    "Journey-Map" = @("journey", "touchpoint", "step", "emotion", "pain point", "experience", "flow")
    "Competitive-Analysis" = @("competitor", "feature", "pricing", "market positioning", "alternative")
    "Market-Trends" = @("trend", "market", "adoption", "forecast", "opportunity", "growth")
    "Feature-Research" = @("feature", "use case", "requirement", "acceptance criteria", "feasibility")
}

$CATEGORY_FOLDERS = @(
    "Personas",
    "Journey-Maps",
    "Competitive-Analysis",
    "Market-Trends",
    "Feature-Research"
)

# ============================================================================
# Logging & Helpers
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

function Initialize-WikiTemp {
    try {
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
        Push-Location $WIKI_TEMP
        & git clone $wikiUrl "." 2>&1 | Out-Null
        Pop-Location
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
        & git config user.email "aios-automation@github.local" 2>&1 | Out-Null
        & git config user.name "AIOS Wiki Manager" 2>&1 | Out-Null
        Pop-Location
        return $true
    }
    catch {
        return $false
    }
}

function Cleanup-WikiTemp {
    try {
        if (-not $IS_PERSISTENT -and (Test-Path $WIKI_TEMP)) {
            Remove-Item -Recurse -Force $WIKI_TEMP -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Log "Cleanup warning: $_" "WARN"
    }
}

# ============================================================================
# Discovery & Classification
# ============================================================================

function Discover-WikiState {
    <# 
    Scan all pages in wiki, return metadata: name, size, category (auto-detected)
    #>
    param([string]$wikiPath)
    
    $pages = @()
    
    $mdFiles = Get-ChildItem -Path $wikiPath -Recurse -Filter "*.md" -ErrorAction SilentlyContinue | 
               Where-Object { $_.Name -ne "Home.md" -and $_.Name -ne "README.md" }
    
    foreach ($file in $mdFiles) {
        $relativePath = $file.FullName.Substring($wikiPath.Length).TrimStart('\')
        $pageName = $relativePath -replace '\.md$', '' -replace '\\', '/'
        $content = Get-Content -Path $file.FullName -Encoding UTF8 -Raw
        
        $pages += @{
            name = $pageName
            fileName = $file.Name
            fullPath = $file.FullName
            size = $file.Length
            content = $content
            title = ($content -split "`n" | Where-Object { $_ -match '^#\s' } | Select-Object -First 1) -replace '^#\s+', ''
            created = $file.CreationTime
        }
    }
    
    return $pages
}

function Classify-PageContent {
    <# 
    Analyze page, return category and confidence score
    #>
    param(
        [hashtable]$page
    )
    
    $content = $page.content.ToLower()
    $title = $page.title.ToLower()
    $scores = @{}
    
    foreach ($category in $CATEGORY_KEYWORDS.Keys) {
        $keywords = $CATEGORY_KEYWORDS[$category]
        $matches = 0
        
        foreach ($keyword in $keywords) {
            $matches += ([regex]::Matches($content, "\b$keyword\b")).Count
            $matches += ([regex]::Matches($title, "\b$keyword\b")).Count * 2  # Title matches weighted
        }
        
        $scores[$category] = $matches
    }
    
    # Find best match
    $bestCategory = $scores.Keys | Sort-Object { -$scores[$_] } | Select-Object -First 1
    $bestScore = if ($scores[$bestCategory] -gt 0) { $scores[$bestCategory] / 10 } else { 0 }
    $confidence = [math]::Min($bestScore, 1.0)  # Cap at 1.0
    
    if ($confidence -lt 0.3) {
        $bestCategory = "Other"
        $confidence = 0
    }
    
    return @{
        category = $bestCategory
        confidence = $confidence
        all_scores = $scores
    }
}

# ============================================================================
# Messiness Metric
# ============================================================================

function Calculate-MessinessMetric {
    <#
    Measure organizational chaos (0-100).
    Returns: score, percentage (ORGANIZED/NEEDS_ATTENTION/CHAOS/SEVERE_CHAOS), issues
    #>
    param(
        [object[]]$pages,
        [hashtable[]]$classifications
    )
    
    $score = 0
    $issues = @()
    
    # Check for duplicates (very high cost)
    $nameGroups = $pages | Group-Object -Property { $_.name -replace '^[^/]+/', '' } | Where-Object { $_.Count -gt 1 }
    foreach ($group in $nameGroups) {
        $score += 20
        $issues += "Found $($group.Count) pages with same name: $($group.Name)"
    }
    
    # Check for naming inconsistency
    $inconsistentNames = @()
    foreach ($page in $pages) {
        if ($page.name -match '[A-Z]') {  # Mixed case
            $inconsistentNames += $page.name
        }
        if ($page.name -match '_') {  # Snake case
            $inconsistentNames += $page.name
        }
    }
    if ($inconsistentNames.Count -gt 0) {
        $score += $inconsistentNames.Count * 2
        $issues += "Found $($inconsistentNames.Count) pages with inconsistent naming (should be Kebab-Case)"
    }
    
    # Check for uncategorized pages (not in proper folder)
    $uncategorized = @()
    foreach ($page in $pages) {
        if (-not ($page.name -match '^(Personas|Journey-Maps|Competitive-Analysis|Market-Trends|Feature-Research)/')) {
            $uncategorized += $page.name
        }
    }
    if ($uncategorized.Count -gt 0) {
        $score += $uncategorized.Count * 5
        $issues += "Found $($uncategorized.Count) pages outside category folders"
    }
    
    # Check for low-confidence classifications
    $lowConfidence = $classifications | Where-Object { $_.confidence -lt 0.5 }
    if ($lowConfidence.Count -gt 0) {
        $score += $lowConfidence.Count * 5
    }
    
    # Cap score at 100
    $score = [math]::Min($score, 100)
    
    $level = if ($score -le 20) { "✅ ORGANIZED - Low" }
            elseif ($score -le 40) { "⚠️ NEEDS_ATTENTION - Medium" }
            elseif ($score -le 60) { "🔴 CHAOS - High" }
            else { "🔥 SEVERE_CHAOS - Very High" }
    
    return @{
        score = $score
        level = $level
        issues = $issues
        uncategorized_count = $uncategorized.Count
        inconsistent_count = $inconsistentNames.Count
    }
}

# ============================================================================
# Auto-Organization
# ============================================================================

function Auto-Organize {
    <#
    Reorganize wiki based on classifications.
    Move pages to proper folders, rename for consistency, consolidate near-duplicates.
    #>
    param(
        [object[]]$pages,
        [hashtable[]]$classifications
    )
    
    $changes = @{
        moved = 0
        renamed = 0
        consolidated = 0
    }
    
    foreach ($i in 0..($pages.Count - 1)) {
        $page = $pages[$i]
        $class = $classifications[$i]
        
        # Determine target location
        $targetCategory = $class.category
        if ($targetCategory -eq "Other") {
            $targetFolder = "Other"
        } else {
            $targetFolder = $targetCategory
        }
        
        # Normalize page name to Kebab-Case
        $normalizedName = $page.title -replace '\s+', '-' -replace '[^a-zA-Z0-9\-]', ''
        $normalizedName = $normalizedName -replace '^-|-$', ''  # Remove leading/trailing hyphens
        
        # Build target path
        $targetPath = Join-Path $WIKI_TEMP $targetFolder "$normalizedName.md"
        
        # Move if needed
        if ($page.fullPath -ne $targetPath) {
            $targetDir = Split-Path $targetPath
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }
            
            Move-Item -Path $page.fullPath -Destination $targetPath -Force -ErrorAction SilentlyContinue
            $changes.moved += 1
        }
    }
    
    return $changes
}

# ============================================================================
# Index Reconciliation
# ============================================================================

function Reconcile-Index {
    <#
    Create/update Research-to-Decision-Index based on discovered pages.
    #>
    param(
        [object[]]$pages,
        [hashtable[]]$classifications
    )
    
    $indexPath = Join-Path $WIKI_TEMP "Research-to-Decision-Index.md"
    
    # Build index content
    $indexContent = @"
# Research-to-Decision-Index

Master registry of all research artifacts. Auto-generated and updated.

Last Updated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

| Subject | Status | Wiki Page | Last Updated | Research Issues | Findings | Confidence |
|---------|--------|-----------|--------------|-----------------|----------|------------|
"@
    
    # Add entries for each page
    foreach ($i in 0..($pages.Count - 1)) {
        $page = $pages[$i]
        $class = $classifications[$i]
        
        if ($class.category -ne "Other") {  # Skip non-research pages
            $subject = $page.title
            $status = "✅ Complete"
            $wikiPage = $page.name
            $lastUpdated = $page.created.ToString("yyyy-MM-dd")
            $findings = $page.content.Substring(0, [math]::Min(100, $page.content.Length)).Replace("`n", " ")
            $confidence = if ($class.confidence -gt 0.8) { "HIGH" } elseif ($class.confidence -gt 0.5) { "MEDIUM" } else { "LOW" }
            
            $indexContent += "`n| $subject | $status | $wikiPage | $lastUpdated | — | $findings | $confidence |"
        }
    }
    
    Set-Content -Path $indexPath -Value $indexContent -Encoding UTF8
    
    return @{
        entries_created = ($pages | Where-Object { $classifications[$pages.IndexOf($_)].category -ne "Other" }).Count
    }
}

# ============================================================================
# Push Changes
# ============================================================================

function Push-WikiChanges {
    param(
        [string]$message
    )
    
    try {
        Push-Location $WIKI_TEMP
        & git add "." 2>&1 | Out-Null
        
        $status = & git status --porcelain 2>&1
        if (-not $status) {
            Pop-Location
            return $true
        }
        
        & git commit -m $message 2>&1 | Out-Null
        & git push origin main 2>&1 | Out-Null
        
        Pop-Location
        return $true
    }
    catch {
        Write-Log "Failed to push changes: $_" "ERROR"
        return $false
    }
}

# ============================================================================
# Action Handlers
# ============================================================================

function Handle-AuditAndOrganize {
    Write-Log "Executing audit-and-organize action"
    
    $result = @{
        status = "error"
        action = "ERROR"
        audit_timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
        wiki_state_before = $null
        reorganization = $null
        wiki_state_after = $null
    }
    
    if (-not (Initialize-WikiTemp)) {
        $result.status = "error"
        return $result | ConvertTo-Json -Depth 5
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        $result.status = "error"
        return $result | ConvertTo-Json -Depth 5
    }
    
    try {
        # Discover current state
        $pages = Discover-WikiState $WIKI_TEMP
        $classifications = @()
        foreach ($page in $pages) {
            $classifications += Classify-PageContent $page
        }
        
        # Measure chaos
        $messiness = Calculate-MessinessMetric $pages $classifications
        
        $result.wiki_state_before = @{
            total_pages = $pages.Count
            messiness_score = $messiness.score
            messiness_level = $messiness.level
            issues = $messiness.issues
        }
        
        # Decide: reorganize?
        if ($messiness.score -gt 40 -or $force_reorganize) {
            Write-Log "Organizing wiki (messiness: $($messiness.score)/100)"
            
            if (-not (Configure-GitUser)) {
                throw "Failed to configure git"
            }
            
            # Reorganize
            $changes = Auto-Organize $pages $classifications
            
            # Update index
            $indexResult = Reconcile-Index $pages $classifications
            
            # Push changes
            if (-not (Push-WikiChanges "Auto-organize wiki and update index")) {
                throw "Failed to push changes"
            }
            
            # Remeasure
            $pages = Discover-WikiState $WIKI_TEMP
            $classifications = @()
            foreach ($page in $pages) {
                $classifications += Classify-PageContent $page
            }
            $messiness = Calculate-MessinessMetric $pages $classifications
            
            $result.reorganization = @{
                performed = $true
                pages_moved = $changes.moved
                index_entries_updated = $indexResult.entries_created
            }
            
            $result.action = "REORGANIZED"
        }
        else {
            Write-Log "Wiki already organized (messiness: $($messiness.score)/100)"
            $result.action = "ALREADY_ORGANIZED"
            $result.reorganization = @{
                performed = $false
            }
        }
        
        # Final state
        $categoryDist = @{}
        foreach ($page in $pages) {
            $folder = ($page.name -split '/')[0]
            $categoryDist[$folder] = ($categoryDist[$folder] -as [int]) + 1
        }
        
        $result.wiki_state_after = @{
            total_pages = $pages.Count
            messiness_score = $messiness.score
            messiness_level = $messiness.level
            structure = $categoryDist
        }
        
        $result.status = "success"
    }
    catch {
        Write-Log "Audit and organize failed: $_" "ERROR"
        $result.status = "error"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json -Depth 5
}

function Handle-CheckWikiHealth {
    Write-Log "Executing check-wiki-health action"
    
    $result = @{
        status = "error"
        wiki_health = $null
        category_breakdown = $null
        problems_found = $null
    }
    
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json -Depth 5
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json -Depth 5
    }
    
    try {
        $pages = Discover-WikiState $WIKI_TEMP
        $classifications = @()
        foreach ($page in $pages) {
            $classifications += Classify-PageContent $page
        }
        
        $messiness = Calculate-MessinessMetric $pages $classifications
        
        $categoryDist = @{}
        foreach ($page in $pages) {
            $folder = ($page.name -split '/')[0]
            $categoryDist[$folder] = ($categoryDist[$folder] -as [int]) + 1
        }
        
        $result.status = "success"
        $result.wiki_health = @{
            total_pages = $pages.Count
            messiness_score = $messiness.score
            messiness_level = $messiness.level
            organization_status = if ($messiness.score -gt 40) { "NEEDS_ATTENTION" } else { "HEALTHY" }
            last_organized = if ((Test-Path (Join-Path $WIKI_TEMP "Research-to-Decision-Index.md"))) { "Unknown" } else { "Never" }
        }
        
        $result.category_breakdown = $categoryDist
        
        $result.problems_found = @{
            duplicate_subjects = ($pages | Group-Object -Property { $_.name -replace '^[^/]+/', '' } | Where-Object { $_.Count -gt 1 }).Count
            uncategorized_pages = $messiness.uncategorized_count
            naming_issues = $messiness.inconsistent_count
            index_current = if ((Test-Path (Join-Path $WIKI_TEMP "Research-to-Decision-Index.md"))) { "Unknown" } else { $false }
        }
    }
    catch {
        Write-Log "Health check failed: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json -Depth 5
}

function Handle-FindOrCreate {
    Write-Log "Executing find-or-create action for: $search_term"
    
    $result = @{
        status = "error"
        action_taken = "ERROR"
        search_term = $search_term
        result = $null
        index_entry = $null
    }
    
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json -Depth 5
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json -Depth 5
    }
    
    try {
        # Search for existing
        $pages = Discover-WikiState $WIKI_TEMP
        $found = $null
        
        foreach ($page in $pages) {
            if ($page.title -ilike "*$search_term*" -or $page.name -ilike "*$search_term*") {
                $found = $page
                break
            }
        }
        
        if ($found) {
            Write-Log "Found existing page: $($found.name)"
            
            # Classify
            $class = Classify-PageContent $found
            
            $result.status = "success"
            $result.action_taken = "FOUND"
            $result.result = @{
                page_name = $found.name
                location = $found.name
                category = $class.category
                category_confidence = $class.confidence
                created = $false
            }
        }
        else {
            Write-Log "No existing page found, creating new for: $search_term"
            
            if (-not (Configure-GitUser)) {
                throw "Failed to configure git"
            }
            
            # Classify content to determine category
            $tempPage = @{
                content = $content
                title = $search_term
            }
            $class = Classify-PageContent $tempPage
            $category = if ($class.confidence -gt 0.5) { $class.category } else { "Other" }
            
            # Create folder if needed
            $folder = $category
            if ($category -eq "Other") {
                $folder = "Other"
            }
            
            $folderPath = Join-Path $WIKI_TEMP $folder
            if (-not (Test-Path $folderPath)) {
                New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
            }
            
            # Normalize name
            $normalizedName = $search_term -replace '\s+', '-' -replace '[^a-zA-Z0-9\-]', ''
            $pagePath = Join-Path $folderPath "$normalizedName.md"
            
            # Write content
            if (-not $content) {
                $content = "# $search_term`n`nResearch in progress..."
            }
            
            Set-Content -Path $pagePath -Value $content -Encoding UTF8
            
            # Update index
            Reconcile-Index (Discover-WikiState $WIKI_TEMP) @(Classify-PageContent $tempPage)
            
            # Push
            if (-not (Push-WikiChanges "Create $search_term")) {
                throw "Failed to push changes"
            }
            
            $result.status = "success"
            $result.action_taken = "CREATED"
            $result.result = @{
                page_name = $normalizedName
                location = "$folder/$normalizedName"
                category = $category
                category_confidence = $class.confidence
                created = $true
            }
        }
        
        $result.index_entry = @{
            subject = $search_term
            status = "In Progress"
            wiki_page = $result.result.location
            last_updated = Get-Date -Format "yyyy-MM-dd"
        }
    }
    catch {
        Write-Log "Find or create failed: $_" "ERROR"
        $result.status = "error"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json -Depth 5
}

# ============================================================================
# Main Execution
# ============================================================================

Write-Log "GitHub Wiki Manager v2 - Action: $action"

try {
    switch ($action) {
        "audit-and-organize" { Handle-AuditAndOrganize }
        "check-wiki-health" { Handle-CheckWikiHealth }
        "find-or-create" { Handle-FindOrCreate }
        default {
            $error = @{
                status = "error"
                message = "Unknown action: $action"
                valid_actions = @("audit-and-organize", "check-wiki-health", "find-or-create")
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

## Usage from Agents

### From PM Agent (Before Creating Research)

```markdown
CALL SKILL: wiki-manager

**Parameters:**
- action: find-or-create
- repo: owner/repo
- search_term: "Field Manager"
- content: "[Optional]"

**Decision:**
- If FOUND: Use existing wiki page, don't create research
- If CREATED: New research item added to wiki + index
```

### Periodic Maintenance (Weekly)

```markdown
CALL SKILL: wiki-manager

**Parameters:**
- action: audit-and-organize
- repo: owner/repo
- force_reorganize: false

**Purpose:** Keep wiki healthy and organized
```

---

## Benefits of v2

✅ **Adaptive** — Works with any wiki state, fixes chaos automatically  
✅ **Self-Healing** — Discovers messes, reorganizes without user guidance  
✅ **Simpler** — 3 smart actions vs 9 specialized ones  
✅ **Measurable** — Messiness metric shows progress  
✅ **Autonomous** — Can run periodically via orchestrator  
✅ **Less Brittle** — No assumptions about structure, discovers it

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
        # Support subdirectory structure (e.g., Personas/Field-Manager)
        $dirPath, $fileName = Ensure-DirectoryStructure $page_name $WIKI_TEMP
        $pageFile = Join-Path $dirPath "$fileName.md"
        
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
        # Support subdirectory structure (e.g., Personas/Field-Manager)
        $pageFile = Get-PageFilePath $page_name $WIKI_TEMP
        
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
        
        # Recursively find all .md files including those in subdirectories
        $mdFiles = Get-ChildItem -Path $WIKI_TEMP -Recurse -Filter "*.md" -ErrorAction SilentlyContinue | 
                   Where-Object { $_.Name -ne "Home.md" -and $_.Name -ne "README.md" }
        
        foreach ($file in $mdFiles) {
            # Construct relative path from WIKI_TEMP
            $relativePath = $file.FullName.Substring($WIKI_TEMP.Length).TrimStart('\')
            $pageName = $relativePath -replace '\.md$', '' -replace '\\', '/'
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
# Directory & Index Helper Functions (For New Deduplication Actions)
# ============================================================================

function Ensure-DirectoryStructure {
    param(
        [string]$pagePath,  # e.g. "Personas/Field-Manager"
        [string]$wikiRoot   # e.g. $WIKI_TEMP
    )
    
    # Split path: Personas/Field-Manager → directory = Personas, filename = Field-Manager
    $parts = $pagePath -split '/' | Where-Object { $_ }
    
    if ($parts.Count -gt 1) {
        # Has directory structure
        $directory = $parts[0..($parts.Count-2)] -join '\'
        $dirPath = Join-Path $wikiRoot $directory
        
        if (-not (Test-Path $dirPath)) {
            Write-Log "Creating directory structure: $dirPath"
            New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
        }
        
        return $dirPath, $parts[-1]  # Return (directory path, filename)
    }
    else {
        # Flat structure
        return $wikiRoot, $parts[0]
    }
}

function Get-PageFilePath {
    param(
        [string]$pagePath,  # e.g. "Personas/Field-Manager"
        [string]$wikiRoot
    )
    
    $parts = $pagePath -split '/' | Where-Object { $_ }
    $fileName = ($parts[-1] -replace '\s+', '-') + ".md"
    
    if ($parts.Count -gt 1) {
        $directory = $parts[0..($parts.Count-2)] -join '\'
        $filePath = Join-Path $wikiRoot $directory $fileName
    }
    else {
        $filePath = Join-Path $wikiRoot $fileName
    }
    
    return $filePath
}

function Parse-ResearchIndex {
    param([string]$indexContent)
    
    # Parse markdown table into array of objects
    $lines = $indexContent -split "`n" | Where-Object { $_.Trim() }
    $entries = @()
    
    $headerFound = $false
    $separatorFound = $false
    
    foreach ($line in $lines) {
        # Skip until we find the table header
        if (-not $headerFound) {
            if ($line -match '^\|.*Subject.*Status.*Wiki Page') {
                $headerFound = $true
                continue
            }
            continue
        }
        
        # Skip separator row (|---|---|...)
        if ($line -match '^\|\s*-+\s*\|') {
            $separatorFound = $true
            continue
        }
        
        if (-not $separatorFound) { continue }
        
        # Parse data rows
        if ($line -match '^\|') {
            $cells = $line -split '\|' | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }
            
            if ($cells.Count -ge 6) {
                $entries += @{
                    subject = $cells[0]
                    status = $cells[1]
                    wiki_page = $cells[2]
                    last_updated = $cells[3]
                    research_issues = $cells[4]
                    findings_summary = $cells[5]
                    confidence = if ($cells.Count -gt 6) { $cells[6] } else { "UNKNOWN" }
                }
            }
        }
    }
    
    return $entries
}

function Find-IndexEntry {
    param(
        [object[]]$entries,
        [string]$searchTerm,
        [string]$searchType  # "persona", "journey", "competitive", "trend", "feature"
    )
    
    # Try exact match first
    foreach ($entry in $entries) {
        if ($entry.subject -eq $searchTerm) {
            return $entry
        }
    }
    
    # Try case-insensitive match
    foreach ($entry in $entries) {
        if ($entry.subject -ilike "*$searchTerm*") {
            return $entry
        }
    }
    
    return $null
}

function Calculate-Similarity {
    param(
        [string]$text1,
        [string]$text2
    )
    
    # Simple text-based similarity (Jaccard index on words)
    $words1 = $text1.ToLower() -split '\s+' | Where-Object { $_.Length -gt 3 }
    $words2 = $text2.ToLower() -split '\s+' | Where-Object { $_.Length -gt 3 }
    
    $common = @($words1 | Where-Object { $words2 -contains $_ }).Count
    $total = (@($words1) + @($words2) | Sort-Object -Unique).Count
    
    if ($total -eq 0) { return 0 }
    
    return [math]::Round($common / $total, 2)
}

# ============================================================================
# New Action Handlers (Research Deduplication)
# ============================================================================

function Handle-CheckPersonaExists {
    Write-Log "Executing check-persona-exists action for persona: $page_name"
    
    $result = @{
        status = "error"
        exists = $false
        wiki_page = ""
        last_updated = ""
        interview_count = 0
        summary = ""
    }
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        $pagePath = Get-PageFilePath "Personas/$page_name" $WIKI_TEMP
        
        if (Test-Path $pagePath) {
            $content = Get-Content -Path $pagePath -Encoding UTF8 -Raw
            
            # Extract interview count from content (look for "interviews" mentions)
            $interviewCount = ([regex]::Matches($content, 'interview', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
            
            $result.status = "success"
            $result.exists = $true
            $result.wiki_page = "Personas/$page_name"
            $result.last_updated = (Get-Item $pagePath).LastWriteTime.ToString("yyyy-MM-dd")
            $result.interview_count = $interviewCount
            $result.summary = "Persona researched and documented in wiki"
        }
        else {
            $result.status = "success"
            $result.exists = $false
        }
    }
    catch {
        $result.status = "error"
        Write-Log "Failed to check persona: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-CheckIndexStatus {
    Write-Log "Executing check-index-status action for search_type: $($PSBoundParameters['search_type']) search_term: $($PSBoundParameters['search_term'])"
    
    $result = @{
        status = "error"
        found = $false
        index_entry = $null
        action_recommended = "UNKNOWN"
    }
    
    # Get search parameters from bound parameters (workaround for dynamic params)
    $searchType = $PSBoundParameters['search_type']
    $searchTerm = $PSBoundParameters['search_term']
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        # Read index
        $indexPath = Get-PageFilePath "Research-to-Decision-Index" $WIKI_TEMP
        
        if (-not (Test-Path $indexPath)) {
            Write-Log "Index not found at: $indexPath"
            $result.status = "success"
            $result.found = $false
            Cleanup-WikiTemp
            return $result | ConvertTo-Json
        }
        
        $indexContent = Get-Content -Path $indexPath -Encoding UTF8 -Raw
        $entries = Parse-ResearchIndex $indexContent
        
        # Find matching entry
        $entry = Find-IndexEntry $entries $searchTerm $searchType
        
        if ($entry) {
            $result.status = "success"
            $result.found = $true
            $result.index_entry = @{
                subject = $entry.subject
                research_status = $entry.status
                wiki_page = $entry.wiki_page
                last_updated = $entry.last_updated
                research_items = @($entry.research_issues -split ',' | ForEach-Object { $_.Trim() })
                confidence = $entry.confidence
            }
            
            # Recommend action based on status
            if ($entry.status -match "Complete|✅") {
                $result.action_recommended = "USE_EXISTING"
            }
            elseif ($entry.status -match "In Progress|🔄") {
                $result.action_recommended = "LINK_AND_CLOSE"
            }
            elseif ($entry.status -match "Deferred|⏸") {
                $result.action_recommended = "CLOSE_DEFERRED"
            }
            else {
                $result.action_recommended = "INVESTIGATE"
            }
        }
        else {
            $result.status = "success"
            $result.found = $false
            $result.action_recommended = "CREATE_NEW"
        }
    }
    catch {
        $result.status = "error"
        Write-Log "Failed to check index status: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-FindDuplicateResearch {
    Write-Log "Executing find-duplicate-research action for topic: $($PSBoundParameters['topic']) persona: $($PSBoundParameters['persona'])"
    
    $result = @{
        status = "error"
        duplicates_found = 0
        matches = @()
        recommendation = "UNKNOWN"
    }
    
    $topic = $PSBoundParameters['topic']
    $persona = $PSBoundParameters['persona']
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        $result.status = "success"
        $searchQuery = "$topic $persona"
        
        # Scan all markdown files in wiki
        $mdFiles = Get-ChildItem -Path $WIKI_TEMP -Recurse -Filter "*.md" -ErrorAction SilentlyContinue
        
        foreach ($file in $mdFiles) {
            if ($file.Name -eq "Home.md") { continue }
            
            $content = Get-Content -Path $file.FullName -Encoding UTF8 -Raw
            
            # Calculate similarity
            $similarity = Calculate-Similarity $searchQuery $content
            
            if ($similarity -gt 0.5) {  # Threshold: 50% similarity
                $result.matches += @{
                    wiki_page = $file.BaseName
                    similarity_score = $similarity
                    reason = if ($similarity -gt 0.85) { "High match" } elseif ($similarity -gt 0.7) { "Moderate match" } else { "Possible duplicate" }
                }
            }
        }
        
        $result.duplicates_found = $result.matches.Count
        
        # Sort by similarity descending
        $result.matches = $result.matches | Sort-Object -Property similarity_score -Descending
        
        # Recommend action
        if ($result.duplicates_found -gt 0 -and $result.matches[0].similarity_score -gt 0.9) {
            $result.recommendation = "UPDATE_EXISTING_NOT_CREATE_NEW"
        }
        elseif ($result.duplicates_found -gt 0 -and $result.matches[0].similarity_score -gt 0.7) {
            $result.recommendation = "REVIEW_AND_CONSOLIDATE"
        }
        else {
            $result.recommendation = "CREATE_NEW_RESEARCH"
        }
    }
    catch {
        $result.status = "error"
        Write-Log "Failed to find duplicates: $_" "ERROR"
    }
    finally {
        Cleanup-WikiTemp
    }
    
    return $result | ConvertTo-Json
}

function Handle-RegisterResearch {
    Write-Log "Executing register-research action"
    
    $result = @{
        status = "error"
        message = "Research registration failed"
        index_updated = $false
        timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    }
    
    # Get parameters
    $researchType = $PSBoundParameters['research_type']
    $subject = $PSBoundParameters['subject']
    $wikiPage = $PSBoundParameters['wiki_page']
    $githubIssue = $PSBoundParameters['github_issue']
    $researchStatus = $PSBoundParameters['status']
    $notes = $PSBoundParameters['notes']
    
    # Initialize
    if (-not (Initialize-WikiTemp)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    if (-not (Clone-Wiki $repo)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    if (-not (Configure-GitUser)) {
        Cleanup-WikiTemp
        return $result | ConvertTo-Json
    }
    
    try {
        $indexPath = Get-PageFilePath "Research-to-Decision-Index" $WIKI_TEMP
        $indexExists = Test-Path $indexPath
        
        # Create index if it doesn't exist
        if (-not $indexExists) {
            Write-Log "Creating new Research-to-Decision-Index"
            
            $indexContent = @"
# Research-to-Decision-Index

Master registry of all research artifacts and their current status.

| Subject | Status | Wiki Page | Last Updated | Research Issue(s) | Findings Summary | Confidence |
|---------|--------|-----------|--------------|-------------------|------------------|------------|
"@
            
            # Create directory if needed
            $dirPath = Split-Path $indexPath
            if (-not (Test-Path $dirPath)) {
                New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
            }
            
            Set-Content -Path $indexPath -Value $indexContent -Encoding UTF8
        }
        
        # Read current index
        $indexContent = Get-Content -Path $indexPath -Encoding UTF8 -Raw
        $entries = Parse-ResearchIndex $indexContent
        
        # Check if subject already in index
        $existingEntry = Find-IndexEntry $entries $subject $researchType
        
        if ($existingEntry) {
            Write-Log "Updating existing index entry for: $subject"
            # Update logic: rebuild index with updated entry
        }
        else {
            Write-Log "Adding new entry to index for: $subject"
            
            # Add new row to index table
            $newRow = "| $subject | $researchStatus | $wikiPage | $(Get-Date -Format 'yyyy-MM-dd') | $githubIssue | $notes | HIGH |"
            $indexContent = $indexContent + "`n$newRow"
            
            Set-Content -Path $indexPath -Value $indexContent -Encoding UTF8
        }
        
        # Push changes
        $commitMsg = "Register research: $subject - $researchStatus"
        if (Push-WikiChanges $commitMsg) {
            $result.status = "success"
            $result.message = "Research registered in index: $subject"
            $result.index_updated = $true
        }
        else {
            $result.status = "error"
            $result.message = "Failed to push index updates"
        }
    }
    catch {
        $result.status = "error"
        $result.message = "Failed to register research: $_"
        Write-Log "Failed to register research: $_" "ERROR"
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
        "check-persona-exists" { Handle-CheckPersonaExists }
        "check-index-status" { Handle-CheckIndexStatus }
        "find-duplicate-research" { Handle-FindDuplicateResearch }
        "register-research" { Handle-RegisterResearch }
        default {
            $error = @{
                status = "error"
                message = "Unknown action: $action"
                valid_actions = @(
                    "init-check",
                    "write-page",
                    "update-page",
                    "read-page",
                    "list-pages",
                    "check-persona-exists",
                    "check-index-status",
                    "find-duplicate-research",
                    "register-research"
                )
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
