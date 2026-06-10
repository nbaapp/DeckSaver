"""
DeckSaver design-tracker helper (Google Sheets API via service account).

Key file (service-account JSON) is read from %USERPROFILE%\\.decksaver\\sheets-key.json
by default, or from the DECKSAVER_SHEETS_KEY env var if set. The key lives outside
the git repo so it can never be committed.

The JSON payload for append/update can be passed inline OR, to avoid shell-quoting
pain on Windows, as "@path\\to\\file.json" (a leading @ means "read from this file").

Usage:
  python sheets_tool.py tabs
  python sheets_tool.py read   "Orders"
  python sheets_tool.py append "Orders" @row.json
  python sheets_tool.py update "Orders" "Scorching" @changes.json
  python sheets_tool.py append "Orders" "{\"Name\":\"Scorching\"}"   # inline also works

- "append" maps the JSON object to columns by header name (column order/insertions don't matter).
- "update" finds the row whose "Name" matches and sets only the given columns.
- The header row is auto-detected (first row in the top 6 containing a "Name" cell),
  so tabs with a "Sort By:" banner row (like the Elements tab) work too.
"""
import csv
import json
import os
import sys

import gspread
from google.oauth2.service_account import Credentials

SHEET_ID = "1oaXdaxt7pDUsq6es5q3GMOf6FEWp9kCE8fIdV8-_lz0"
SCOPES = ["https://www.googleapis.com/auth/spreadsheets"]


def _key_path():
    p = os.environ.get("DECKSAVER_SHEETS_KEY")
    if p:
        return p
    return os.path.join(os.path.expanduser("~"), ".decksaver", "sheets-key.json")


def _client():
    creds = Credentials.from_service_account_file(_key_path(), scopes=SCOPES)
    return gspread.authorize(creds)


def _open():
    return _client().open_by_key(SHEET_ID)


def _load_payload(arg):
    """Parse a JSON payload given inline or as @path/to/file.json."""
    if arg.startswith("@"):
        with open(arg[1:], encoding="utf-8-sig") as f:  # utf-8-sig tolerates a BOM
            return json.load(f)
    return json.loads(arg)


def _header_row(ws):
    probe = ws.get_values("A1:Z6")
    for i, row in enumerate(probe):
        if any(str(c).strip() == "Name" for c in row):
            return i + 1
    return 1


def _headers(ws, header_row):
    return ws.row_values(header_row)


def cmd_tabs():
    for ws in _open().worksheets():
        print(f"{ws.id}\t{ws.title}")


def cmd_read(tab):
    ws = _open().worksheet(tab)
    hr = _header_row(ws)
    headers = _headers(ws, hr)
    rows = ws.get_all_values()[hr:]
    print(json.dumps({"tab": tab, "headers": headers, "rows": rows}, indent=2, ensure_ascii=False))


def cmd_append(tab, values_json):
    values = _load_payload(values_json)
    ws = _open().worksheet(tab)
    hr = _header_row(ws)
    headers = _headers(ws, hr)
    row = [values.get(h, "") for h in headers]
    ws.append_row(row, value_input_option="USER_ENTERED")
    unknown = [k for k in values if k not in headers]
    print(json.dumps({"ok": True, "action": "append", "tab": tab,
                      "ignored_unknown_columns": unknown}, ensure_ascii=False))


def cmd_update(tab, name, values_json):
    values = _load_payload(values_json)
    ws = _open().worksheet(tab)
    hr = _header_row(ws)
    headers = _headers(ws, hr)
    name_col = headers.index("Name")
    data = ws.get_all_values()
    for r in range(hr, len(data)):
        if str(data[r][name_col]).strip() == str(name).strip():
            updates = []
            for k, v in values.items():
                if k in headers:
                    c = headers.index(k)
                    updates.append({"range": gspread.utils.rowcol_to_a1(r + 1, c + 1), "values": [[v]]})
            if updates:
                ws.batch_update(updates, value_input_option="USER_ENTERED")
            print(json.dumps({"ok": True, "action": "update", "tab": tab,
                              "name": name, "row": r + 1}, ensure_ascii=False))
            return
    print(json.dumps({"ok": False, "error": f"name not found: {name}"}, ensure_ascii=False))


def cmd_sync(out_dir):
    """Dump every tab to <out_dir>/<tab>.csv via the authenticated API (no link-sharing needed)."""
    os.makedirs(out_dir, exist_ok=True)
    for ws in _open().worksheets():
        path = os.path.join(out_dir, ws.title + ".csv")
        with open(path, "w", newline="", encoding="utf-8") as f:
            csv.writer(f).writerows(ws.get_all_values())
        print(f"OK   {ws.title}")


def cmd_add_column(tab, header_name):
    ws = _open().worksheet(tab)
    hr = _header_row(ws)
    headers = _headers(ws, hr)
    if header_name in headers:
        print(json.dumps({"ok": True, "action": "add-column", "tab": tab,
                          "header": header_name, "note": "already exists, skipped"},
                         ensure_ascii=False))
        return
    new_col = len(headers) + 1
    if new_col > ws.col_count:
        ws.add_cols(new_col - ws.col_count)
    ws.update_cell(hr, new_col, header_name)
    print(json.dumps({"ok": True, "action": "add-column", "tab": tab,
                      "header": header_name, "col": new_col}, ensure_ascii=False))


def cmd_delete(tab, name):
    ws = _open().worksheet(tab)
    hr = _header_row(ws)
    headers = _headers(ws, hr)
    name_col = headers.index("Name")
    data = ws.get_all_values()
    for r in range(hr, len(data)):
        if str(data[r][name_col]).strip() == str(name).strip():
            ws.delete_rows(r + 1)
            print(json.dumps({"ok": True, "action": "delete", "tab": tab,
                              "name": name, "row": r + 1}, ensure_ascii=False))
            return
    print(json.dumps({"ok": False, "error": f"name not found: {name}"}, ensure_ascii=False))


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    cmd = args[0]
    if cmd == "tabs":
        cmd_tabs()
    elif cmd == "sync":
        cmd_sync(args[1] if len(args) > 1 else os.path.dirname(os.path.abspath(__file__)))
    elif cmd == "read":
        cmd_read(args[1])
    elif cmd == "append":
        cmd_append(args[1], args[2])
    elif cmd == "update":
        cmd_update(args[1], args[2], args[3])
    elif cmd == "delete":
        cmd_delete(args[1], args[2])
    elif cmd == "add-column":
        cmd_add_column(args[1], args[2])
    else:
        print(f"unknown command: {cmd}\n{__doc__}")


if __name__ == "__main__":
    main()
