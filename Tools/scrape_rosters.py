#!/usr/bin/env python3
"""
Scrape Basketball Reference team roster pages to get draft info and college for all players.
Then scrape Spotrac for contract data.
Much faster than individual player pages - 30 team pages instead of 505 player pages.
"""

import json
import time
import re
import os
import urllib.request

PLAYERS_JSON = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'StreamingAssets', 'Data', 'players.json')
OUTPUT_FILE = os.path.join(os.path.dirname(__file__), 'scraped_data.json')

BR_TEAMS = {
    "ATL": "ATL", "BOS": "BOS", "BKN": "BRK", "CHA": "CHO", "CHI": "CHI",
    "CLE": "CLE", "DAL": "DAL", "DEN": "DEN", "DET": "DET", "GSW": "GSW",
    "HOU": "HOU", "IND": "IND", "LAC": "LAC", "LAL": "LAL", "MEM": "MEM",
    "MIA": "MIA", "MIL": "MIL", "MIN": "MIN", "NOP": "NOP", "NYK": "NYK",
    "OKC": "OKC", "ORL": "ORL", "PHI": "PHI", "PHX": "PHO", "POR": "POR",
    "SAC": "SAC", "SAS": "SAS", "TOR": "TOR", "UTA": "UTA", "WAS": "WAS"
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
                time.sleep(3)
            else:
                print(f"  FAILED: {url} - {e}")
                return None

def parse_roster_page(html, team_abbr):
    """Parse BR roster page to extract player info."""
    players = {}
    if not html:
        return players

    # Find the roster table
    # BR roster tables have columns: No., Player, Pos, Ht, Wt, Birth Date, ?, Exp, College
    # The table id is "roster"
    table_match = re.search(r'<table[^>]*id="roster"[^>]*>(.*?)</table>', html, re.DOTALL)
    if not table_match:
        print(f"  No roster table found for {team_abbr}")
        return players

    table_html = table_match.group(1)
    rows = re.findall(r'<tr[^>]*>(.*?)</tr>', table_html, re.DOTALL)

    for row in rows:
        cells = re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', row, re.DOTALL)
        if len(cells) < 7:
            continue

        # Extract player name from link
        name_match = re.search(r'<a[^>]*>([^<]+)</a>', cells[1])
        if not name_match:
            continue
        full_name = name_match.group(1).strip()

        # Extract college
        college = ""
        if len(cells) > 7:
            college_match = re.search(r'<a[^>]*>([^<]+)</a>', cells[-1])
            if college_match:
                college = college_match.group(1).strip()
            else:
                college = re.sub(r'<[^>]+>', '', cells[-1]).strip()

        players[full_name] = {
            "college": college,
            "team": team_abbr
        }

    return players

def parse_player_page_for_draft(html):
    """Parse individual BR player page for draft info."""
    result = {"draft_year": 0, "draft_round": 0, "draft_pick": 0, "drafted_by": ""}
    if not html:
        return result

    draft_match = re.search(
        r'Draft:</strong>\s*<a[^>]*>([^<]+)</a>.*?(\d+)\w{2}\s+round.*?(\d+)\w{2}\s+overall.*?(\d{4})\s+NBA',
        html, re.DOTALL
    )
    if draft_match:
        result["drafted_by"] = draft_match.group(1).strip()
        result["draft_round"] = int(draft_match.group(2))
        result["draft_pick"] = int(draft_match.group(3))
        result["draft_year"] = int(draft_match.group(4))

    return result

def main():
    with open(PLAYERS_JSON) as f:
        players = json.load(f)

    print(f"Loaded {len(players)} players")

    # Step 1: Scrape all team roster pages for college info
    all_roster_data = {}
    for game_abbr, br_abbr in BR_TEAMS.items():
        url = f"https://www.basketball-reference.com/teams/{br_abbr}/2026.html"
        print(f"Fetching {game_abbr} roster from {url}")
        html = fetch_url(url)
        roster = parse_roster_page(html, game_abbr)
        all_roster_data.update(roster)
        print(f"  Found {len(roster)} players")
        time.sleep(3.5)

    print(f"\nTotal roster entries: {len(all_roster_data)}")

    # Match roster data to our players
    matched = 0
    for p in players:
        full_name = f"{p['FirstName']} {p['LastName']}"
        if full_name in all_roster_data:
            rd = all_roster_data[full_name]
            if rd.get('college'):
                p['College'] = rd['college']
            matched += 1
        else:
            # Try fuzzy matching
            for rname, rd in all_roster_data.items():
                if p['LastName'] in rname and p['FirstName'][:3] in rname:
                    if rd.get('college'):
                        p['College'] = rd['college']
                    matched += 1
                    break

    print(f"Matched {matched}/{len(players)} players from roster pages")

    # Save intermediate
    with open(PLAYERS_JSON, 'w') as f:
        json.dump(players, f, indent=2)

    with open(OUTPUT_FILE, 'w') as f:
        json.dump(all_roster_data, f, indent=2)

    print(f"Saved to {PLAYERS_JSON}")

if __name__ == '__main__':
    main()
