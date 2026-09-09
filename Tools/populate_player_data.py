#!/usr/bin/env python3
"""
Populate players.json with draft info, college, and contract data.
Sources: Basketball Reference for draft/college, Spotrac/BR for contracts.
"""

import json
import time
import re
import os
import sys
import urllib.request
import urllib.error
from html.parser import HTMLParser

PLAYERS_JSON = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'StreamingAssets', 'Data', 'players.json')
CACHE_FILE = os.path.join(os.path.dirname(__file__), 'player_data_cache.json')

# Known draft data for all NBA players as of Dec 2025
# Format: { "firstname_lastname": { "college": "", "draft_year": 0, "draft_round": 0, "draft_pick": 0, "drafted_by": "", "contract_years": 0, "contract_salary": 0 } }

def fetch_url(url):
    """Fetch URL with retries and rate limiting."""
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)'})
            with urllib.request.urlopen(req, timeout=15) as resp:
                return resp.read().decode('utf-8', errors='replace')
        except Exception as e:
            if attempt < 2:
                time.sleep(2)
            else:
                print(f"  Failed to fetch {url}: {e}")
                return None

def get_bbref_slug(first, last, player_id):
    """Generate Basketball Reference URL slug."""
    # BR format: /players/l/lastfi01.html (first 5 of last name + first 2 of first + 01)
    last_clean = re.sub(r"[^a-z]", "", last.lower().replace("'", "").replace("-", ""))
    first_clean = re.sub(r"[^a-z]", "", first.lower().replace("'", "").replace("-", ""))
    if not last_clean or not first_clean:
        return None
    slug = last_clean[:5] + first_clean[:2] + "01"
    letter = last_clean[0]
    return f"https://www.basketball-reference.com/players/{letter}/{slug}.html"

def parse_bbref_page(html, first, last):
    """Parse Basketball Reference player page for draft info and college."""
    result = {
        "college": "",
        "draft_year": 0,
        "draft_round": 0,
        "draft_pick": 0,
        "drafted_by": ""
    }

    if not html:
        return result

    # College - look for "College:" in info box
    college_match = re.search(r'College:</strong>\s*<a[^>]*>([^<]+)</a>', html)
    if college_match:
        result["college"] = college_match.group(1).strip()

    # Pre-NBA team for international players
    if not result["college"]:
        # Check for high school or other pre-NBA info
        hs_match = re.search(r'High School:</strong>\s*([^<]+)', html)
        if hs_match:
            # Don't set college to high school - leave blank or check for pre-draft team
            pass

    # Draft info
    draft_match = re.search(r'Draft:</strong>\s*<a[^>]*>([^<]+)</a>.*?(\d+)\w+\s+round.*?(\d+)\w+\s+overall.*?(\d{4})\s+NBA', html, re.DOTALL)
    if draft_match:
        result["drafted_by"] = draft_match.group(1).strip()
        result["draft_round"] = int(draft_match.group(2))
        result["draft_pick"] = int(draft_match.group(3))
        result["draft_year"] = int(draft_match.group(4))
    else:
        # Try alternate pattern
        draft_match2 = re.search(r'Draft:</strong>.*?(\d{4})\s+NBA\s+Draft.*?Round\s+(\d+).*?Pick\s+(\d+)', html, re.DOTALL | re.IGNORECASE)
        if draft_match2:
            result["draft_year"] = int(draft_match2.group(1))
            result["draft_round"] = int(draft_match2.group(2))
            result["draft_pick"] = int(draft_match2.group(3))
            # Try to get team
            team_match = re.search(r'Draft:</strong>\s*<a[^>]*>([^<]+)</a>', html)
            if team_match:
                result["drafted_by"] = team_match.group(1).strip()

    return result

# Team name to abbreviation mapping
TEAM_ABBREV = {
    "Atlanta Hawks": "ATL", "Boston Celtics": "BOS", "Brooklyn Nets": "BKN",
    "Charlotte Hornets": "CHA", "Chicago Bulls": "CHI", "Cleveland Cavaliers": "CLE",
    "Dallas Mavericks": "DAL", "Denver Nuggets": "DEN", "Detroit Pistons": "DET",
    "Golden State Warriors": "GSW", "Houston Rockets": "HOU", "Indiana Pacers": "IND",
    "Los Angeles Clippers": "LAC", "Los Angeles Lakers": "LAL", "LA Clippers": "LAC",
    "Memphis Grizzlies": "MEM", "Miami Heat": "MIA", "Milwaukee Bucks": "MIL",
    "Minnesota Timberwolves": "MIN", "New Orleans Pelicans": "NOP",
    "New York Knicks": "NYK", "Oklahoma City Thunder": "OKC",
    "Orlando Magic": "ORL", "Philadelphia 76ers": "PHI", "Phoenix Suns": "PHX",
    "Portland Trail Blazers": "POR", "Sacramento Kings": "SAC",
    "San Antonio Spurs": "SAS", "Toronto Raptors": "TOR", "Utah Jazz": "UTA",
    "Washington Wizards": "WAS",
    # Historical
    "Seattle SuperSonics": "OKC", "New Jersey Nets": "BKN",
    "Charlotte Bobcats": "CHA", "Vancouver Grizzlies": "MEM",
    "New Orleans Hornets": "NOP", "New Orleans/Oklahoma City Hornets": "NOP",
}

def team_name_to_abbrev(name):
    """Convert full team name to abbreviation."""
    if not name:
        return ""
    name = name.strip()
    if name in TEAM_ABBREV:
        return TEAM_ABBREV[name]
    # Fuzzy match
    for full, abbr in TEAM_ABBREV.items():
        if name.lower() in full.lower() or full.lower() in name.lower():
            return abbr
    return name

def scrape_player(first, last, player_id, existing_cache):
    """Scrape a single player's data from Basketball Reference."""
    cache_key = player_id
    if cache_key in existing_cache:
        return existing_cache[cache_key]

    url = get_bbref_slug(first, last, player_id)
    if not url:
        return None

    print(f"  Fetching {first} {last} from {url}")
    html = fetch_url(url)

    if html and f"{last}" not in html and f"{first}" not in html:
        # Wrong player page, try 02 suffix
        url = url.replace("01.html", "02.html")
        print(f"  Retrying with {url}")
        html = fetch_url(url)

    result = parse_bbref_page(html, first, last)

    # Rate limit
    time.sleep(3.5)  # BR rate limit is ~20 req/min

    return result


def build_known_data():
    """
    Build a comprehensive dictionary of known NBA player data.
    This avoids needing to scrape 505 players from Basketball Reference.
    We'll use web searches for verification but start with known data.
    """
    # We'll fetch team rosters from BR to get all players at once
    pass


def main():
    with open(PLAYERS_JSON) as f:
        players = json.load(f)

    # Load cache
    cache = {}
    if os.path.exists(CACHE_FILE):
        with open(CACHE_FILE) as f:
            cache = json.load(f)

    print(f"Loaded {len(players)} players, {len(cache)} cached")

    # Process each player
    updated = 0
    for i, p in enumerate(players):
        pid = p['PlayerId']
        first = p['FirstName']
        last = p['LastName']

        if pid in cache:
            data = cache[pid]
        else:
            data = scrape_player(first, last, pid, cache)
            if data:
                cache[pid] = data
                # Save cache periodically
                if len(cache) % 10 == 0:
                    with open(CACHE_FILE, 'w') as f:
                        json.dump(cache, f, indent=2)

        if data:
            if data.get('college'):
                p['College'] = data['college']
            if data.get('draft_year'):
                p['DraftYear'] = data['draft_year']
                p['DraftRound'] = data.get('draft_round', 0)
                p['DraftPick'] = data.get('draft_pick', 0)
                drafted_by = data.get('drafted_by', '')
                p['DraftedByTeamId'] = team_name_to_abbrev(drafted_by)
            updated += 1

        if (i + 1) % 50 == 0:
            print(f"  Progress: {i+1}/{len(players)} ({updated} updated)")

    # Save cache
    with open(CACHE_FILE, 'w') as f:
        json.dump(cache, f, indent=2)

    # Save updated players
    with open(PLAYERS_JSON, 'w') as f:
        json.dump(players, f, indent=2)

    print(f"\nDone! Updated {updated}/{len(players)} players")
    print(f"Cache saved to {CACHE_FILE}")

if __name__ == '__main__':
    main()
