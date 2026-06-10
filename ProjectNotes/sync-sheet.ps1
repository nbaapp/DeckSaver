# Syncs the "DeckSaver Design Tracker" Google Sheet down to CSV files in this folder.
# Uses the authenticated Sheets API (service account) via sheets_tool.py, so the sheet
# does NOT need to be publicly shared. Run:  pwsh -File "ProjectNotes/sync-sheet.ps1"

$python = Join-Path $env:LOCALAPPDATA "Programs\Python\Python313\python.exe"
$tool   = Join-Path $PSScriptRoot "sheets_tool.py"

if (-not (Test-Path $python)) { Write-Error "Python not found at $python"; exit 1 }

& $python $tool sync $PSScriptRoot
