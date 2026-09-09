#!/usr/bin/env python3
"""
Fetch draft info, college, and contract data for all NBA players.
Sources: NBA Stats API (draft/college), Basketball Reference (contracts).
"""

import json
import os
import time
import re
import urllib.request

from nba_api.stats.endpoints import drafthistory, playerindex

PLAYERS_JSON = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'StreamingAssets', 'Data', 'players.json')
OUTPUT_FILE = os.path.join(os.path.dirname(__file__), 'nba_enrichment_data.json')

NBA_TEAM_ID_MAP = {
    1610612737: "ATL", 1610612738: "BOS", 1610612751: "BKN", 1610612766: "CHA",
    1610612741: "CHI", 1610612739: "CLE", 1610612742: "DAL", 1610612743: "DEN",
    1610612765: "DET", 1610612744: "GSW", 1610612745: "HOU", 1610612754: "IND",
    1610612746: "LAC", 1610612747: "LAL", 1610612763: "MEM", 1610612748: "MIA",
    1610612749: "MIL", 1610612750: "MIN", 1610612740: "NOP", 1610612752: "NYK",
    1610612760: "OKC", 1610612753: "ORL", 1610612755: "PHI", 1610612756: "PHX",
    1610612757: "POR", 1610612758: "SAC", 1610612759: "SAS", 1610612761: "TOR",
    1610612762: "UTA", 1610612764: "WAS",
}

BR_TEAM_ABBREVS = [
    "ATL", "BOS", "BRK", "CHO", "CHI", "CLE", "DAL", "DEN", "DET", "GSW",
    "HOU", "IND", "LAC", "LAL", "MEM", "MIA", "MIL", "MIN", "NOP", "NYK",
    "OKC", "ORL", "PHI", "PHO", "POR", "SAC", "SAS", "TOR", "UTA", "WAS"
]

def normalize_name(name):
    name = name.strip().lower()
    for suffix in [' jr.', ' jr', ' sr.', ' sr', ' iii', ' ii', ' iv', ' v']:
        name = name.replace(suffix, '')
    replacements = {'ć': 'c', 'č': 'c', 'ž': 'z', 'š': 's', 'đ': 'd', 'ö': 'o',
                    'ü': 'u', 'é': 'e', 'è': 'e', 'á': 'a', 'à': 'a', 'í': 'i',
                    'ñ': 'n', 'ā': 'a', 'ū': 'u', 'ī': 'i',
                    "'": "", "'": "", "-": " ", ".": "", "'": ""}
    for k, v in replacements.items():
        name = name.replace(k, v)
    return ' '.join(name.split())

def fetch_url(url):
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={
                'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36'
            })
            with urllib.request.urlopen(req, timeout=15) as resp:
                return resp.read().decode('utf-8', errors='replace')
        except Exception as e:
            if attempt < 2:
                time.sleep(3)
            else:
                print(f"  FAILED: {url} - {e}")
                return None

def fetch_player_index():
    """Get college/draft from NBA stats player index."""
    print("Fetching player index from NBA stats API...")
    pi_data = {}
    try:
        pi = playerindex.PlayerIndex(season="2024-25", league_id="00")
        time.sleep(1)
        data = pi.get_dict()
        headers = data['resultSets'][0]['headers']
        rows = data['resultSets'][0]['rowSet']
        print(f"  Got {len(rows)} players")
        for row in rows:
            d = dict(zip(headers, row))
            name_key = normalize_name(f"{d.get('PLAYER_FIRST_NAME', '')} {d.get('PLAYER_LAST_NAME', '')}")
            pi_data[name_key] = {
                "college": d.get('COLLEGE', '') or '',
                "draft_year": d.get('DRAFT_YEAR', ''),
                "draft_round": d.get('DRAFT_ROUND', ''),
                "draft_number": d.get('DRAFT_NUMBER', ''),
            }
    except Exception as e:
        print(f"  Failed: {e}")
    return pi_data

def fetch_draft_history():
    """Fetch all draft picks 2000-2024."""
    all_picks = {}
    for year in range(2000, 2025):
        try:
            dh = drafthistory.DraftHistory(league_id="00", season_year_nullable=year)
            time.sleep(0.7)
            data = dh.get_dict()
            headers = data['resultSets'][0]['headers']
            rows = data['resultSets'][0]['rowSet']
            for row in rows:
                d = dict(zip(headers, row))
                name_key = normalize_name(d.get('PLAYER_NAME', ''))
                if name_key:
                    team_id = d.get('TEAM_ID', 0)
                    all_picks[name_key] = {
                        "draft_year": int(d.get('SEASON', year)),
                        "draft_round": int(d.get('ROUND_NUMBER', 0)),
                        "draft_pick": int(d.get('OVERALL_PICK', 0)),
                        "drafted_by": NBA_TEAM_ID_MAP.get(team_id, ""),
                        "organization": d.get('ORGANIZATION', '')
                    }
            if year % 5 == 0:
                print(f"  Fetched drafts 2000-{year}...")
        except Exception as e:
            print(f"  Draft {year} failed: {e}")
    print(f"  Total draft picks: {len(all_picks)}")
    return all_picks

def fetch_br_contracts():
    """Fetch contracts from Basketball Reference team contract pages."""
    all_contracts = {}
    for br_abbr in BR_TEAM_ABBREVS:
        url = f"https://www.basketball-reference.com/contracts/{br_abbr}.html"
        print(f"  Fetching {br_abbr} contracts...")
        html = fetch_url(url)
        if not html:
            continue

        table = re.search(r'<table[^>]*id="contracts"[^>]*>(.*?)</table>', html, re.DOTALL)
        if not table:
            print(f"    No contracts table for {br_abbr}")
            continue

        rows = re.findall(r'<tr[^>]*>(.*?)</tr>', table.group(1), re.DOTALL)
        for row in rows:
            cells = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', row, re.DOTALL)
            if len(cells) < 3:
                continue

            # First cell should have player name in <a> tag
            name_match = re.search(r'<a[^>]*>([^<]+)</a>', cells[0])
            if not name_match:
                continue
            name = name_match.group(1).strip()
            name_key = normalize_name(name)

            # Parse salary columns - cells[2] onwards are year salaries
            # Format: "$30,666,666" or empty
            salaries_by_year = []
            for cell in cells[2:]:
                clean = re.sub(r'<[^>]+>', '', cell).strip()
                salary_match = re.search(r'\$([\d,]+)', clean)
                if salary_match:
                    sal = int(salary_match.group(1).replace(',', ''))
                    salaries_by_year.append(sal)
                else:
                    salaries_by_year.append(0)

            # Current salary = first year, years remaining = count of non-zero years
            current_salary = salaries_by_year[0] if salaries_by_year else 0
            years_remaining = sum(1 for s in salaries_by_year if s > 0)

            if current_salary > 0:
                all_contracts[name_key] = {
                    "salary": current_salary,
                    "years_remaining": max(1, years_remaining),
                }

        time.sleep(3.5)  # BR rate limit

    print(f"  Total contracts: {len(all_contracts)}")
    return all_contracts

def fuzzy_match(name_key, lookup_dict):
    """Try fuzzy matching by last name + first initial."""
    if name_key in lookup_dict:
        return lookup_dict[name_key]
    parts = name_key.split()
    if len(parts) >= 2:
        last = parts[-1]
        first_init = parts[0][:3]
        for k, v in lookup_dict.items():
            kparts = k.split()
            if len(kparts) >= 2 and kparts[-1] == last and kparts[0][:3] == first_init:
                return v
    return None

def main():
    with open(PLAYERS_JSON) as f:
        raw = json.load(f)
    if isinstance(raw, dict) and 'Players' in raw:
        players = raw['Players']
        wrapper = raw
    else:
        players = raw
        wrapper = None
    print(f"Loaded {len(players)} players")

    # Step 1: Player index (college + draft for current players)
    pi_data = fetch_player_index()

    # Step 2: Draft history (comprehensive)
    print("\nFetching draft history...")
    draft_data = fetch_draft_history()

    # Step 3: Contracts from Basketball Reference
    print("\nFetching contracts from Basketball Reference...")
    contract_data = fetch_br_contracts()

    # Step 4: Match and update
    print("\n--- Matching ---")
    draft_ok = college_ok = contract_ok = 0
    no_draft = []
    no_college = []
    no_contract = []

    for p in players:
        name_key = normalize_name(f"{p['FirstName']} {p['LastName']}")

        # --- Draft ---
        draft_info = None
        # Check draft history first (most complete)
        dd = fuzzy_match(name_key, draft_data)
        if dd and dd.get("draft_year", 0) > 0:
            draft_info = dd
        # Also check player index
        pi = fuzzy_match(name_key, pi_data)
        if pi and not draft_info:
            try:
                dy = int(pi['draft_year']) if pi['draft_year'] and str(pi['draft_year']) != 'Undrafted' else 0
                if dy > 0:
                    draft_info = {
                        "draft_year": dy,
                        "draft_round": int(pi.get('draft_round', 0) or 0),
                        "draft_pick": int(pi.get('draft_number', 0) or 0),
                        "drafted_by": "",
                    }
            except (ValueError, TypeError):
                pass

        if draft_info and int(draft_info.get("draft_year", 0) or 0) > 0:
            p["DraftYear"] = int(draft_info["draft_year"])
            p["DraftRound"] = int(draft_info.get("draft_round", 0) or 0)
            p["DraftPick"] = int(draft_info.get("draft_pick", 0) or 0)
            p["DraftedByTeamId"] = draft_info.get("drafted_by", "")
            draft_ok += 1
        else:
            no_draft.append(f"{p['FirstName']} {p['LastName']} ({p['TeamId']})")

        # --- College ---
        college = ""
        if pi and pi.get('college'):
            college = pi['college']
        elif dd and dd.get('organization'):
            college = dd['organization']

        if college and college.lower() not in ('', 'none', 'n/a'):
            p["College"] = college
            college_ok += 1
        elif "College" not in p:
            no_college.append(f"{p['FirstName']} {p['LastName']} ({p['TeamId']})")

        # --- Contract ---
        cd = fuzzy_match(name_key, contract_data)
        if cd and cd.get("salary", 0) > 0:
            p["ContractSalary"] = cd["salary"]
            p["ContractYears"] = cd["years_remaining"]
            contract_ok += 1
        else:
            no_contract.append(f"{p['FirstName']} {p['LastName']} ({p['TeamId']})")

    print(f"\nResults:")
    print(f"  Draft:    {draft_ok}/{len(players)}")
    print(f"  College:  {college_ok}/{len(players)}")
    print(f"  Contract: {contract_ok}/{len(players)}")

    if no_draft:
        print(f"\n  Missing draft ({len(no_draft)}):")
        for n in no_draft[:20]:
            print(f"    {n}")
    if no_contract:
        print(f"\n  Missing contract ({len(no_contract)}):")
        for n in no_contract[:20]:
            print(f"    {n}")

    # Save debug data
    with open(OUTPUT_FILE, 'w') as f:
        json.dump({"no_draft": no_draft, "no_college": no_college, "no_contract": no_contract}, f, indent=2)

    # Save
    if wrapper:
        wrapper['Players'] = players
        with open(PLAYERS_JSON, 'w') as f:
            json.dump(wrapper, f, indent=2)
    else:
        with open(PLAYERS_JSON, 'w') as f:
            json.dump(players, f, indent=2)

    print(f"\nDone! Saved to {PLAYERS_JSON}")

if __name__ == '__main__':
    main()
