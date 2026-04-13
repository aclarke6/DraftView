<# 
Copy-SourceFiles.ps1

Purpose
  Copies the contents of source files into the clipboard, preceded by a header
  containing the file name, repo-relative path, and full path.

  Each argument may be:
    - A bare filename:        Reader.css
    - A bare filename stem:   Reader
    - A filename wildcard:    *Reader*.cs
    - A repo-relative path:   DraftView.Web\wwwroot\css\Reader.css

  Bare filenames are located by recursive search under RootPath.
  Bare filename stems are resolved by searching a designated list of standard
  C# solution file extensions.
  Wildcards are allowed at filename level only and are matched against file names
  during recursive search under RootPath.
  Relative paths are resolved directly against RootPath - no search needed.

Usage
  .\Copy-SourceFiles.ps1 <file1> <file2> ... [options]
  .\Copy-SourceFiles.ps1 -Names <file1,file2,...> [options]
  .\Copy-SourceFiles.ps1 -Files <file1,file2,...> [options]

Examples
  .\Copy-SourceFiles.ps1 Program.cs VaultReader.cs
  .\Copy-SourceFiles.ps1 DraftView.Web\wwwroot\css\Reader.css
  .\Copy-SourceFiles.ps1 Reader.css DraftView.Web\Views\Reader\Read.cshtml
  .\Copy-SourceFiles.ps1 -RootPath C:\Users\alast\source\repos\DraftView Program.cs
  .\Copy-SourceFiles.ps1 -RootPath . -OutputFile Dump.txt Program.cs VaultReader.cs
  .\Copy-SourceFiles.ps1 -h
#>

[CmdletBinding()]
param(
    [Alias("h")]
    [switch]$ShowHelp,

    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [Alias("Names", "Files")]
    [string[]]$FileNames,

    [Parameter(Mandatory = $false)]
    [string]$RootPath = ".",

    [Parameter(Mandatory = $false)]
    [string]$OutputFile = "",

    [Parameter(Mandatory = $false)]
    [int]$ChunkLimit = 100000
)

if ($ShowHelp -or -not $FileNames -or @($FileNames).Count -eq 0) {
    Write-Host @"
Copy-SourceFiles.ps1
-------------------
Copies the contents of source files into the clipboard, preceded by a header
containing the file name, repo-relative path, and full path.

Each argument may be a bare filename (Reader.css), a bare filename stem (Reader),
a filename wildcard (*Reader*.cs), or a repo-relative path
(DraftView.Web\wwwroot\css\Reader.css).

USAGE
  .\Copy-SourceFiles.ps1 <file1> <file2> ... [options]
  .\Copy-SourceFiles.ps1 -Names <file1,file2,...> [options]
  .\Copy-SourceFiles.ps1 -Files <file1,file2,...> [options]

OPTIONS
  -Names | -Files | (positional)
      One or more filenames or repo-relative paths.

  -RootPath <path>
      Root directory to search from. Default: current directory (.)

  -OutputFile <path>
      If provided, also writes each chunk to a numbered file (UTF-8).

  -ChunkLimit <int>
      Maximum characters per clipboard chunk. Default: 100000

  -h | -ShowHelp
      Show this help text and exit.

NOTES
  Put -RootPath, -OutputFile and -ChunkLimit BEFORE positional arguments:
    .\Copy-SourceFiles.ps1 -RootPath . -OutputFile Dump.txt Program.cs
"@
    exit 0
}

$ErrorActionPreference = "Stop"

$excludeDirs = @("bin", "obj", ".git", ".vs", ".idea", "TestResults", "node_modules")
$defaultExtensions = @(
    ".cs",
    ".cshtml",
    ".razor",
    ".css",
    ".js",
    ".ts",
    ".json",
    ".config",
    ".xml",
    ".xaml",
    ".sql",
    ".ps1",
    ".props",
    ".targets",
    ".sln",
    ".slnx",
    ".csproj"
)

if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
    throw "RootPath '$RootPath' is not a directory. Pass -RootPath explicitly."
}

$root = (Resolve-Path -LiteralPath $RootPath).Path

function Get-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$FullPath,
        [Parameter(Mandatory = $true)][string]$RootPathResolved
    )
    $rootWithSep = $RootPathResolved.TrimEnd('\') + '\'
    if ($FullPath.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($rootWithSep.Length)
    }
    return $FullPath
}

function Is-UnderExcludedDir {
    param([Parameter(Mandatory = $true)][string]$FullPath)
    $parts = $FullPath.Split([IO.Path]::DirectorySeparatorChar)
    foreach ($p in $parts) {
        if ($excludeDirs -contains $p) { return $true }
    }
    return $false
}

function Get-SearchableFiles {
    Get-ChildItem -Path $root -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object {
            -not (Is-UnderExcludedDir -FullPath $_.FullName)
        }
}

function Find-FileByName {
    param([Parameter(Mandatory = $true)][string]$FileName)

    Get-SearchableFiles |
        Where-Object {
            $_.Name -ieq $FileName
        } |
        Sort-Object FullName
}

function Find-FilesByWildcard {
    param([Parameter(Mandatory = $true)][string]$Pattern)

    Get-SearchableFiles |
        Where-Object {
            $_.Name -like $Pattern
        } |
        Sort-Object FullName
}

function Find-FilesByStem {
    param([Parameter(Mandatory = $true)][string]$Stem)

    Get-SearchableFiles |
        Where-Object {
            $baseName = [IO.Path]::GetFileNameWithoutExtension($_.Name)
            $extension = [IO.Path]::GetExtension($_.Name)

            $baseName -ieq $Stem -and
            $defaultExtensions -icontains $extension
        } |
        Sort-Object FullName
}

function Resolve-AmbiguousMatch {
    param(
        [Parameter(Mandatory = $true)][string]$OriginalArgument,
        [AllowEmptyCollection()][System.IO.FileInfo[]]$CandidateFiles = @()
    )

    if ($CandidateFiles.Count -eq 0) {
        return [pscustomobject]@{
            Status  = "Missing"
            Matches = @()
        }
    }

    if ($CandidateFiles.Count -eq 1) {
        return [pscustomobject]@{
            Status  = "Found"
            Matches = @($CandidateFiles[0])
        }
    }

    Write-Host ""
    Write-Host "AMBIGUOUS MATCHES for '$OriginalArgument'" -ForegroundColor Magenta
    Write-Host "Select a file number, or 0 to skip:" -ForegroundColor Yellow

    for ($i = 0; $i -lt $CandidateFiles.Count; $i++) {
        $relative = Get-RepoRelativePath -FullPath $CandidateFiles[$i].FullName -RootPathResolved $root
        Write-Host ("  {0}) {1}" -f ($i + 1), $relative) -ForegroundColor Yellow
    }

    while ($true) {
        $response = Read-Host "Selection"

        if ($response -match '^\d+$') {
            $selection = [int]$response

            if ($selection -eq 0) {
                return [pscustomobject]@{
                    Status  = "Skipped"
                    Matches = @()
                }
            }

            if ($selection -ge 1 -and $selection -le $CandidateFiles.Count) {
                return [pscustomobject]@{
                    Status  = "Found"
                    Matches = @($CandidateFiles[$selection - 1])
                }
            }
        }

        Write-Host "Enter a number from 0 to $($CandidateFiles.Count)." -ForegroundColor Red
    }
}

# Resolve an argument to a structured result with:
#   - Status  = Found | Missing | Skipped
#   - Matches = array of FileInfo
# If the argument contains a path separator it is treated as a repo-relative path
# and resolved directly.
# Otherwise:
#   - wildcard patterns are matched against file names under $root
#   - names with extensions are matched as exact file names
#   - names without extensions are matched as stems against $defaultExtensions
function Resolve-Argument {
    param([Parameter(Mandatory = $true)][string]$Arg)

    $looksLikePath = $Arg.Contains('\') -or $Arg.Contains('/')

    if ($looksLikePath) {
        $normalised = $Arg.Replace('/', '\')
        $candidate  = Join-Path $root $normalised
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [pscustomobject]@{
                Status  = "Found"
                Matches = @(Get-Item -LiteralPath $candidate)
            }
        }

        return [pscustomobject]@{
            Status  = "Missing"
            Matches = @()
        }
    }

    $hasWildcard = $Arg.Contains('*') -or $Arg.Contains('?')
    if ($hasWildcard) {
        $wildcardMatches = @(Find-FilesByWildcard -Pattern $Arg)

        if ($wildcardMatches.Count -eq 0) {
            return [pscustomobject]@{
                Status  = "Missing"
                Matches = @()
            }
        }

        return [pscustomobject]@{
            Status  = "Found"
            Matches = @($wildcardMatches)
        }
    }

    $hasExtension = [IO.Path]::HasExtension($Arg)
    if ($hasExtension) {
        $fileCandidates = @(Find-FileByName -FileName $Arg)
        return (Resolve-AmbiguousMatch -OriginalArgument $Arg -CandidateFiles $fileCandidates)
    }

    $stemCandidates = @(Find-FilesByStem -Stem $Arg)
    return (Resolve-AmbiguousMatch -OriginalArgument $Arg -CandidateFiles $stemCandidates)
}

$currentChunk        = New-Object System.Text.StringBuilder
$script:chunkIndex   = 1
$script:filesInChunk = [System.Collections.Generic.List[string]]::new()

function Emit-Chunk {
    param(
        [string]$Text,
        [bool]$IsFinal = $false,
        [string[]]$FilesIncluded = @()
    )

    if ([string]::IsNullOrWhiteSpace($Text)) { return }

    $wrapped = New-Object System.Text.StringBuilder

    if ($IsFinal) {
        [void]$wrapped.AppendLine("<<< FINAL CHUNK $script:chunkIndex -- all content is included >>>")
    }
    else {
        [void]$wrapped.AppendLine("<<< CHUNK $script:chunkIndex -- more chunks will follow >>>")
    }

    [void]$wrapped.AppendLine("")
    [void]$wrapped.AppendLine($Text)
    [void]$wrapped.AppendLine("")

    if ($IsFinal) {
        [void]$wrapped.AppendLine("<<< END OF FINAL CHUNK -- nothing further will follow >>>")
    }
    else {
        [void]$wrapped.AppendLine("<<< END OF CHUNK $script:chunkIndex -- Wait for more chunks! >>>")
    }

    $finalText = $wrapped.ToString()

    Write-Host ""

    if ($IsFinal) {
        Write-Host "=== FINAL CHUNK #$script:chunkIndex ===" -ForegroundColor Green
    }
    else {
        Write-Host "=== Chunk #$script:chunkIndex ===" -ForegroundColor Cyan
    }

    Write-Host "Characters : $($finalText.Length)" -ForegroundColor DarkGray

    if ($FilesIncluded.Count -gt 0) {
        Write-Host "Files clipped in this chunk:" -ForegroundColor Yellow
        foreach ($f in $FilesIncluded) {
            Write-Host "  + $f" -ForegroundColor Yellow
        }
    }

    $finalText | Set-Clipboard

    if ($OutputFile -ne "") {
        $outPath = [IO.Path]::ChangeExtension($OutputFile, ".$script:chunkIndex.txt")
        $finalText | Out-File -FilePath $outPath -Encoding utf8
        Write-Host "Also wrote : $outPath" -ForegroundColor DarkGray
    }

    if ($IsFinal) {
        Write-Host ""
        Write-Host "All done -- final chunk is in the clipboard." -ForegroundColor Green
        Write-Host ""
    }
    else {
        Write-Host ""
        Write-Host "Copied to clipboard. Press ENTER for next chunk..." -ForegroundColor Cyan
        Write-Host ""
        $null = Read-Host
        $script:chunkIndex++
    }
}

foreach ($name in $FileNames) {

    $fileBlock  = New-Object System.Text.StringBuilder
    $resolution = Resolve-Argument -Arg $name
    $fileMatches = @($resolution.Matches)

    if ($resolution.Status -eq "Missing") {
        Write-Host "  NOT FOUND : $name" -ForegroundColor Red
        [void]$fileBlock.AppendLine("")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("FILE: $name")
        [void]$fileBlock.AppendLine("STATUS: MISSING (not found under $root)")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("")
    }
    elseif ($resolution.Status -eq "Skipped") {
        Write-Host "  SKIPPED : $name" -ForegroundColor DarkYellow
        [void]$fileBlock.AppendLine("")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("FILE: $name")
        [void]$fileBlock.AppendLine("STATUS: SKIPPED")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("")
    }
    elseif ($fileMatches.Count -gt 1) {
        [void]$fileBlock.AppendLine("")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("FILE: $name")
        [void]$fileBlock.AppendLine("STATUS: MULTIPLE FILES")
        [void]$fileBlock.AppendLine("MATCHES:")
        foreach ($m in $fileMatches) {
            $rel = Get-RepoRelativePath -FullPath $m.FullName -RootPathResolved $root
            [void]$fileBlock.AppendLine(" - $rel")
        }
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("")
    }
    else {
        $file     = $fileMatches[0]
        $absolute = $file.FullName
        $relative = Get-RepoRelativePath -FullPath $absolute -RootPathResolved $root

        [void]$fileBlock.AppendLine("")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("FILE NAME: $($file.Name)")
        [void]$fileBlock.AppendLine("RELATIVE : $relative")
        [void]$fileBlock.AppendLine("FULL PATH: $absolute")
        [void]$fileBlock.AppendLine("============================================================")
        [void]$fileBlock.AppendLine("")

        $content = Get-Content -LiteralPath $absolute -Raw
        [void]$fileBlock.AppendLine($content)
    }

    $blockText = $fileBlock.ToString()

    # Flush current chunk if adding this block would exceed the limit
    if ($currentChunk.Length -gt 0 -and ($currentChunk.Length + $blockText.Length) -gt $ChunkLimit) {
        Emit-Chunk -Text $currentChunk.ToString() -IsFinal:$false -FilesIncluded $script:filesInChunk.ToArray()
        $currentChunk.Clear() | Out-Null
        $script:filesInChunk.Clear()
    }

    # If the block itself exceeds the limit, split it across multiple chunks immediately
    if ($blockText.Length -gt $ChunkLimit) {
        $offset = 0
        while ($offset -lt $blockText.Length) {
            $slice  = $blockText.Substring($offset, [Math]::Min($ChunkLimit, $blockText.Length - $offset))
            $offset += $slice.Length
            $more   = $offset -lt $blockText.Length
            if ($more) {
                Emit-Chunk -Text $slice -IsFinal:$false -FilesIncluded @($name)
            }
            else {
                [void]$currentChunk.Append($slice)
                $script:filesInChunk.Add($name)
            }
        }
    }
    else {
        [void]$currentChunk.Append($blockText)
        $script:filesInChunk.Add($name)
    }
}

Emit-Chunk -Text $currentChunk.ToString() -IsFinal:$true -FilesIncluded $script:filesInChunk.ToArray()
