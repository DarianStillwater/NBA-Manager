#!/usr/bin/env python3
"""
Fix remaining players missing draft/college/contract data.
Uses Basketball Reference individual player pages for the ~150 players that weren't matched.
"""

import json
import os
import time
import re
import urllib.request

PLAYERS_JSON = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'StreamingAssets', 'Data', 'players.json')

TEAM_ABBREV = {
    "Atlanta Hawks": "ATL", "Boston Celtics": "BOS", "Brooklyn Nets": "BKN",
    "Charlotte Hornets": "CHA", "Charlotte Bobcats": "CHA",
    "Chicago Bulls": "CHI", "Cleveland Cavaliers": "CLE",
    "Dallas Mavericks": "DAL", "Denver Nuggets": "DEN", "Detroit Pistons": "DET",
    "Golden State Warriors": "GSW", "Houston Rockets": "HOU", "Indiana Pacers": "IND",
    "Los Angeles Clippers": "LAC", "LA Clippers": "LAC",
    "Los Angeles Lakers": "LAL", "Memphis Grizzlies": "MEM",
    "Miami Heat": "MIA", "Milwaukee Bucks": "MIL", "Minnesota Timberwolves": "MIN",
    "New Orleans Pelicans": "NOP", "New Orleans Hornets": "NOP",
    "New York Knicks": "NYK", "Oklahoma City Thunder": "OKC",
    "Seattle SuperSonics": "OKC", "Orlando Magic": "ORL",
    "Philadelphia 76ers": "PHI", "Phoenix Suns": "PHX",
    "Portland Trail Blazers": "POR", "Sacramento Kings": "SAC",
    "San Antonio Spurs": "SAS", "Toronto Raptors": "TOR",
    "Utah Jazz": "UTA", "Washington Wizards": "WAS",
    "New Jersey Nets": "BKN", "Vancouver Grizzlies": "MEM",
}

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
                time.sleep(2)
            else:
                return None

def team_name_to_abbrev(name):
    if not name:
        return ""
    name = name.strip()
    if name in TEAM_ABBREV:
        return TEAM_ABBREV[name]
    for full, abbr in TEAM_ABBREV.items():
        if name.lower() in full.lower() or full.lower() in name.lower():
            return abbr
    return name

def get_br_slugs(first, last):
    """Generate possible BR URL slugs for a player."""
    last_clean = re.sub(r"[^a-z]", "", last.lower().replace("'", "").replace("'", "").replace("-", ""))
    first_clean = re.sub(r"[^a-z]", "", first.lower().replace("'", "").replace("'", "").replace("-", ""))
    if not last_clean or not first_clean:
        return []
    letter = last_clean[0]
    slugs = []
    for suffix in ["01", "02", "03"]:
        slug = last_clean[:5] + first_clean[:2] + suffix
        slugs.append(f"https://www.basketball-reference.com/players/{letter}/{slug}.html")
    return slugs

def parse_br_player(html, first, last):
    """Parse BR player page for draft, college, and contract info."""
    result = {
        "college": "",
        "draft_year": 0, "draft_round": 0, "draft_pick": 0, "drafted_by": "",
        "salary": 0, "years": 0
    }
    if not html:
        return result

    # Verify it's the right player
    if last.lower() not in html.lower():
        return result

    # College
    college_match = re.search(r'College:</strong>\s*<a[^>]*>([^<]+)</a>', html)
    if college_match:
        result["college"] = college_match.group(1).strip()

    # Draft
    draft_match = re.search(
        r'Draft:</strong>\s*<a[^>]*>([^<]+)</a>.*?(\d+)\w{2}\s+round.*?(\d+)\w{2}\s+overall.*?(\d{4})\s+NBA',
        html, re.DOTALL
    )
    if draft_match:
        result["drafted_by"] = team_name_to_abbrev(draft_match.group(1).strip())
        result["draft_round"] = int(draft_match.group(2))
        result["draft_pick"] = int(draft_match.group(3))
        result["draft_year"] = int(draft_match.group(4))

    # Current salary from contracts table
    contract_table = re.search(r'<table[^>]*id="contracts_[^"]*"[^>]*>(.*?)</table>', html, re.DOTALL)
    if contract_table:
        rows = re.findall(r'<tr[^>]*>(.*?)</tr>', contract_table.group(1), re.DOTALL)
        for row in rows:
            cells = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', row, re.DOTALL)
            if len(cells) >= 2:
                salary_cells = []
                for cell in cells[1:]:
                    clean = re.sub(r'<[^>]+>', '', cell).strip()
                    sal_match = re.search(r'\$([\d,]+)', clean)
                    if sal_match:
                        salary_cells.append(int(sal_match.group(1).replace(',', '')))
                    else:
                        salary_cells.append(0)
                if salary_cells and salary_cells[0] > 0:
                    result["salary"] = salary_cells[0]
                    result["years"] = sum(1 for s in salary_cells if s > 0)

    return result

def main():
    with open(PLAYERS_JSON) as f:
        raw = json.load(f)
    players = raw['Players'] if isinstance(raw, dict) and 'Players' in raw else raw
    wrapper = raw if isinstance(raw, dict) and 'Players' in raw else None

    # Find players still missing data
    needs_fix = []
    for p in players:
        missing_draft = p.get('DraftYear', 0) == 0 and p.get('DraftRound', 0) == 0
        missing_college = not p.get('College', '')
        missing_contract = p.get('ContractSalary', 0) == 0
        if missing_draft or missing_college or missing_contract:
            needs_fix.append(p)

    print(f"Players needing fixes: {len(needs_fix)}")

    fixed_draft = 0
    fixed_college = 0
    fixed_contract = 0

    for i, p in enumerate(needs_fix):
        slugs = get_br_slugs(p['FirstName'], p['LastName'])
        result = None

        for slug_url in slugs:
            html = fetch_url(slug_url)
            if html and p['LastName'].lower()[:4] in html.lower():
                result = parse_br_player(html, p['FirstName'], p['LastName'])
                if result and (result['college'] or result['draft_year'] or result['salary']):
                    break
            time.sleep(3.5)

        if result:
            if result['college'] and not p.get('College'):
                p['College'] = result['college']
                fixed_college += 1
            if result['draft_year'] > 0 and p.get('DraftYear', 0) == 0:
                p['DraftYear'] = result['draft_year']
                p['DraftRound'] = result['draft_round']
                p['DraftPick'] = result['draft_pick']
                p['DraftedByTeamId'] = result['drafted_by']
                fixed_draft += 1
            if result['salary'] > 0 and p.get('ContractSalary', 0) == 0:
                p['ContractSalary'] = result['salary']
                p['ContractYears'] = result['years']
                fixed_contract += 1
            print(f"  [{i+1}/{len(needs_fix)}] {p['FirstName']} {p['LastName']}: college={result['college']}, draft={result['draft_year']}, salary=${result['salary']:,}")
        else:
            print(f"  [{i+1}/{len(needs_fix)}] {p['FirstName']} {p['LastName']}: NO DATA FOUND")

        if (i + 1) % 20 == 0:
            # Save periodically
            if wrapper:
                wrapper['Players'] = players
                with open(PLAYERS_JSON, 'w') as f:
                    json.dump(wrapper, f, indent=2)

    # Final save
    if wrapper:
        wrapper['Players'] = players
        with open(PLAYERS_JSON, 'w') as f:
            json.dump(wrapper, f, indent=2)
    else:
        with open(PLAYERS_JSON, 'w') as f:
            json.dump(players, f, indent=2)

    print(f"\nFixed: draft={fixed_draft}, college={fixed_college}, contract={fixed_contract}")

    # Summary
    total_draft = sum(1 for p in players if p.get('DraftYear', 0) > 0)
    total_college = sum(1 for p in players if p.get('College', ''))
    total_contract = sum(1 for p in players if p.get('ContractSalary', 0) > 0)
    print(f"Totals: draft={total_draft}/505, college={total_college}/505, contract={total_contract}/505")

if __name__ == '__main__':
    main()
