using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Generates real NBA player data with stats.
    /// Contains current 2024-25 rosters for all 30 teams.
    /// </summary>
    public static class NBAPlayerData
    {
        // Real NBA players organized by team with generated stats
        public static List<PlayerData> GetAllPlayers()
        {
            var players = new List<PlayerData>();
            var rng = new System.Random(42); // Consistent seed
            
            // EASTERN CONFERENCE
            
            // Atlanta Hawks
            players.AddRange(GenerateTeamRoster("ATL", new[] {
                ("Trae", "Young", 11, 1, 26, 73, 180, 94), // PG, elite playmaker
                ("Dejounte", "Murray", 5, 1, 28, 76, 180, 84),
                ("Bogdan", "Bogdanovic", 13, 2, 32, 78, 205, 78),
                ("Jalen", "Johnson", 1, 4, 23, 81, 220, 79),
                ("Clint", "Capela", 15, 5, 30, 82, 240, 76),
                ("De'Andre", "Hunter", 12, 3, 26, 80, 225, 75),
                ("Onyeka", "Okongwu", 17, 5, 23, 81, 235, 74),
                ("Saddiq", "Bey", 41, 3, 25, 80, 215, 73)
            }, rng));
            
            // Boston Celtics
            players.AddRange(GenerateTeamRoster("BOS", new[] {
                ("Jayson", "Tatum", 0, 3, 26, 80, 210, 95), // MVP candidate
                ("Jaylen", "Brown", 7, 2, 28, 78, 223, 91),
                ("Derrick", "White", 9, 1, 30, 76, 190, 83),
                ("Jrue", "Holiday", 4, 1, 34, 76, 205, 82),
                ("Kristaps", "Porzingis", 8, 5, 29, 87, 240, 85),
                ("Al", "Horford", 42, 5, 38, 81, 240, 76),
                ("Payton", "Pritchard", 11, 1, 26, 73, 195, 74),
                ("Sam", "Hauser", 30, 3, 27, 80, 220, 72)
            }, rng));
            
            // Brooklyn Nets
            players.AddRange(GenerateTeamRoster("BKN", new[] {
                ("Mikal", "Bridges", 1, 3, 28, 78, 209, 82),
                ("Cameron", "Johnson", 2, 3, 28, 80, 210, 78),
                ("Nic", "Claxton", 33, 5, 25, 83, 215, 77),
                ("Dennis", "Schroder", 17, 1, 31, 73, 172, 75),
                ("Dorian", "Finney-Smith", 28, 4, 31, 80, 220, 74),
                ("Day'Ron", "Sharpe", 20, 5, 23, 83, 265, 72),
                ("Cam", "Thomas", 24, 2, 23, 75, 210, 78),
                ("Ben", "Simmons", 10, 4, 28, 83, 240, 74)
            }, rng));
            
            // Charlotte Hornets
            players.AddRange(GenerateTeamRoster("CHA", new[] {
                ("LaMelo", "Ball", 1, 1, 23, 80, 180, 84),
                ("Brandon", "Miller", 24, 3, 22, 81, 200, 80),
                ("Miles", "Bridges", 0, 4, 26, 79, 225, 79),
                ("Mark", "Williams", 5, 5, 22, 84, 242, 76),
                ("Terry", "Rozier", 3, 1, 30, 73, 190, 77),
                ("P.J.", "Washington", 25, 4, 26, 80, 230, 76),
                ("Gordon", "Hayward", 20, 3, 34, 80, 225, 74),
                ("Nick", "Richards", 14, 5, 26, 83, 245, 72)
            }, rng));
            
            // Chicago Bulls
            players.AddRange(GenerateTeamRoster("CHI", new[] {
                ("DeMar", "DeRozan", 11, 3, 35, 78, 220, 85),
                ("Zach", "LaVine", 8, 2, 29, 77, 200, 87),
                ("Nikola", "Vucevic", 9, 5, 34, 84, 260, 80),
                ("Coby", "White", 0, 1, 24, 77, 195, 78),
                ("Patrick", "Williams", 44, 4, 23, 80, 215, 76),
                ("Alex", "Caruso", 6, 1, 30, 77, 186, 75),
                ("Ayo", "Dosunmu", 12, 1, 24, 76, 200, 74),
                ("Andre", "Drummond", 3, 5, 31, 83, 279, 73)
            }, rng));
            
            // Cleveland Cavaliers
            players.AddRange(GenerateTeamRoster("CLE", new[] {
                ("Donovan", "Mitchell", 45, 2, 28, 73, 215, 92),
                ("Darius", "Garland", 10, 1, 24, 73, 192, 86),
                ("Evan", "Mobley", 4, 4, 23, 84, 215, 85),
                ("Jarrett", "Allen", 31, 5, 26, 83, 243, 82),
                ("Max", "Strus", 1, 2, 28, 78, 215, 76),
                ("Caris", "LeVert", 3, 3, 30, 78, 205, 75),
                ("Isaac", "Okoro", 35, 3, 23, 78, 225, 74),
                ("Dean", "Wade", 32, 4, 27, 81, 228, 71)
            }, rng));
            
            // Detroit Pistons
            players.AddRange(GenerateTeamRoster("DET", new[] {
                ("Cade", "Cunningham", 2, 1, 23, 78, 220, 84),
                ("Jaden", "Ivey", 23, 1, 22, 76, 195, 79),
                ("Ausar", "Thompson", 9, 3, 21, 78, 204, 76),
                ("Jalen", "Duren", 0, 5, 21, 83, 250, 78),
                ("Bojan", "Bogdanovic", 44, 3, 35, 80, 226, 75),
                ("Alec", "Burks", 5, 2, 33, 78, 214, 72),
                ("Isaiah", "Stewart", 28, 4, 23, 81, 250, 74),
                ("Killian", "Hayes", 7, 1, 23, 77, 195, 68)
            }, rng));
            
            // Indiana Pacers
            players.AddRange(GenerateTeamRoster("IND", new[] {
                ("Tyrese", "Haliburton", 0, 1, 24, 77, 185, 90),
                ("Pascal", "Siakam", 43, 4, 30, 81, 230, 85),
                ("Myles", "Turner", 33, 5, 28, 83, 250, 80),
                ("Buddy", "Hield", 7, 2, 31, 76, 220, 78),
                ("Aaron", "Nesmith", 23, 3, 24, 78, 213, 75),
                ("Bennedict", "Mathurin", 00, 2, 22, 78, 210, 77),
                ("Andrew", "Nembhard", 2, 1, 24, 78, 193, 74),
                ("Obi", "Toppin", 1, 4, 26, 81, 220, 74)
            }, rng));
            
            // Miami Heat
            players.AddRange(GenerateTeamRoster("MIA", new[] {
                ("Jimmy", "Butler", 22, 3, 35, 79, 230, 89),
                ("Bam", "Adebayo", 13, 5, 27, 81, 255, 87),
                ("Tyler", "Herro", 14, 2, 24, 77, 199, 82),
                ("Terry", "Rozier", 2, 1, 30, 73, 190, 77),
                ("Duncan", "Robinson", 55, 3, 30, 79, 215, 74),
                ("Caleb", "Martin", 16, 3, 28, 77, 205, 75),
                ("Kevin", "Love", 42, 4, 36, 82, 251, 73),
                ("Jaime", "Jaquez Jr.", 11, 3, 23, 79, 215, 74)
            }, rng));
            
            // Milwaukee Bucks
            players.AddRange(GenerateTeamRoster("MIL", new[] {
                ("Giannis", "Antetokounmpo", 34, 4, 29, 83, 243, 97), // MVP tier
                ("Damian", "Lillard", 0, 1, 34, 74, 195, 90),
                ("Khris", "Middleton", 22, 3, 33, 80, 222, 82),
                ("Brook", "Lopez", 11, 5, 36, 84, 282, 79),
                ("Bobby", "Portis", 9, 4, 29, 82, 250, 77),
                ("Malik", "Beasley", 5, 2, 28, 76, 187, 75),
                ("Pat", "Connaughton", 24, 2, 31, 77, 209, 72),
                ("MarJon", "Beauchamp", 0, 3, 23, 78, 199, 70)
            }, rng));
            
            // New York Knicks
            players.AddRange(GenerateTeamRoster("NYK", new[] {
                ("Jalen", "Brunson", 11, 1, 28, 73, 190, 88),
                ("Julius", "Randle", 30, 4, 29, 80, 250, 84),
                ("OG", "Anunoby", 8, 3, 27, 80, 232, 82),
                ("RJ", "Barrett", 9, 3, 24, 78, 214, 78),
                ("Mitchell", "Robinson", 23, 5, 26, 84, 240, 77),
                ("Donte", "DiVincenzo", 0, 2, 27, 76, 203, 76),
                ("Josh", "Hart", 3, 2, 29, 77, 215, 75),
                ("Isaiah", "Hartenstein", 55, 5, 26, 84, 250, 76)
            }, rng));
            
            // Orlando Magic
            players.AddRange(GenerateTeamRoster("ORL", new[] {
                ("Paolo", "Banchero", 5, 4, 22, 82, 250, 86),
                ("Franz", "Wagner", 22, 3, 23, 82, 220, 83),
                ("Jalen", "Suggs", 4, 1, 23, 76, 205, 78),
                ("Wendell", "Carter Jr.", 34, 5, 25, 82, 270, 77),
                ("Cole", "Anthony", 50, 1, 24, 74, 185, 76),
                ("Markelle", "Fultz", 20, 1, 26, 76, 209, 74),
                ("Gary", "Harris", 14, 2, 30, 76, 210, 72),
                ("Jonathan", "Isaac", 1, 4, 26, 83, 230, 74)
            }, rng));
            
            // Philadelphia 76ers
            players.AddRange(GenerateTeamRoster("PHI", new[] {
                ("Joel", "Embiid", 21, 5, 30, 84, 280, 95), // MVP tier
                ("Tyrese", "Maxey", 0, 1, 24, 74, 200, 86),
                ("Tobias", "Harris", 12, 4, 32, 81, 226, 79),
                ("Kelly", "Oubre Jr.", 9, 3, 28, 79, 203, 77),
                ("De'Anthony", "Melton", 8, 1, 26, 76, 200, 75),
                ("Buddy", "Hield", 17, 2, 31, 76, 220, 77),
                ("Paul", "George", 8, 3, 34, 80, 220, 86),
                ("Kyle", "Lowry", 7, 1, 38, 72, 196, 74)
            }, rng));
            
            // Toronto Raptors
            players.AddRange(GenerateTeamRoster("TOR", new[] {
                ("Scottie", "Barnes", 4, 4, 23, 81, 225, 84),
                ("RJ", "Barrett", 9, 3, 24, 78, 214, 78),
                ("Immanuel", "Quickley", 5, 1, 25, 75, 188, 77),
                ("Jakob", "Poeltl", 19, 5, 29, 84, 245, 76),
                ("Gary", "Trent Jr.", 33, 2, 25, 77, 209, 76),
                ("Chris", "Boucher", 25, 4, 31, 81, 200, 72),
                ("Gradey", "Dick", 1, 2, 20, 78, 205, 73),
                ("Dennis", "Schroder", 17, 1, 31, 73, 172, 75)
            }, rng));
            
            // Washington Wizards
            players.AddRange(GenerateTeamRoster("WAS", new[] {
                ("Jordan", "Poole", 13, 1, 25, 76, 194, 78),
                ("Kyle", "Kuzma", 33, 4, 29, 81, 221, 79),
                ("Deni", "Avdija", 9, 3, 23, 81, 210, 74),
                ("Tyus", "Jones", 5, 1, 28, 73, 196, 74),
                ("Daniel", "Gafford", 21, 5, 26, 83, 234, 75),
                ("Marvin", "Bagley III", 35, 4, 25, 83, 235, 72),
                ("Corey", "Kispert", 24, 3, 25, 79, 223, 73),
                ("Bilal", "Coulibaly", 0, 3, 19, 79, 197, 72)
            }, rng));
            
            // WESTERN CONFERENCE
            
            // Dallas Mavericks
            players.AddRange(GenerateTeamRoster("DAL", new[] {
                ("Luka", "Doncic", 77, 1, 25, 79, 230, 96), // MVP tier
                ("Kyrie", "Irving", 11, 1, 32, 74, 195, 89),
                ("Dereck", "Lively II", 2, 5, 20, 85, 230, 77),
                ("P.J.", "Washington", 25, 4, 26, 80, 230, 77),
                ("Tim", "Hardaway Jr.", 10, 2, 32, 78, 205, 75),
                ("Josh", "Green", 8, 3, 24, 78, 200, 73),
                ("Maxi", "Kleber", 42, 4, 32, 82, 240, 72),
                ("Dante", "Exum", 0, 1, 29, 78, 214, 72)
            }, rng));
            
            // Denver Nuggets
            players.AddRange(GenerateTeamRoster("DEN", new[] {
                ("Nikola", "Jokic", 15, 5, 29, 83, 284, 98), // Best player
                ("Jamal", "Murray", 27, 1, 27, 76, 215, 86),
                ("Michael", "Porter Jr.", 1, 3, 26, 82, 218, 82),
                ("Aaron", "Gordon", 50, 4, 29, 81, 235, 80),
                ("Kentavious", "Caldwell-Pope", 5, 2, 31, 77, 204, 77),
                ("Reggie", "Jackson", 7, 1, 34, 75, 208, 73),
                ("Christian", "Braun", 0, 2, 23, 79, 218, 74),
                ("Peyton", "Watson", 8, 3, 21, 80, 203, 72)
            }, rng));
            
            // Golden State Warriors
            players.AddRange(GenerateTeamRoster("GSW", new[] {
                ("Stephen", "Curry", 30, 1, 36, 74, 185, 93),
                ("Klay", "Thompson", 11, 2, 34, 78, 215, 81),
                ("Andrew", "Wiggins", 22, 3, 29, 79, 197, 80),
                ("Draymond", "Green", 23, 4, 34, 78, 230, 79),
                ("Jonathan", "Kuminga", 00, 4, 21, 80, 225, 78),
                ("Kevon", "Looney", 5, 5, 28, 81, 222, 74),
                ("Chris", "Paul", 3, 1, 39, 72, 175, 77),
                ("Brandin", "Podziemski", 2, 1, 21, 76, 205, 73)
            }, rng));
            
            // Houston Rockets
            players.AddRange(GenerateTeamRoster("HOU", new[] {
                ("Jalen", "Green", 4, 2, 22, 76, 186, 82),
                ("Alperen", "Sengun", 28, 5, 22, 82, 243, 81),
                ("Jabari", "Smith Jr.", 10, 4, 21, 82, 220, 79),
                ("Fred", "VanVleet", 5, 1, 30, 73, 197, 80),
                ("Dillon", "Brooks", 9, 3, 28, 79, 220, 75),
                ("Amen", "Thompson", 1, 1, 21, 78, 206, 74),
                ("Tari", "Eason", 17, 4, 23, 80, 216, 73),
                ("Cam", "Whitmore", 7, 3, 20, 79, 232, 73)
            }, rng));
            
            // Los Angeles Clippers
            players.AddRange(GenerateTeamRoster("LAC", new[] {
                ("Kawhi", "Leonard", 2, 3, 33, 79, 225, 91),
                ("Paul", "George", 13, 3, 34, 80, 220, 86),
                ("James", "Harden", 1, 1, 35, 77, 220, 86),
                ("Russell", "Westbrook", 0, 1, 35, 75, 200, 78),
                ("Ivica", "Zubac", 40, 5, 27, 85, 240, 76),
                ("Norman", "Powell", 24, 2, 31, 76, 215, 75),
                ("Terance", "Mann", 14, 2, 27, 77, 215, 73),
                ("Bones", "Hyland", 5, 1, 24, 75, 173, 73)
            }, rng));
            
            // Los Angeles Lakers
            players.AddRange(GenerateTeamRoster("LAL", new[] {
                ("LeBron", "James", 23, 3, 39, 81, 250, 94),
                ("Anthony", "Davis", 3, 4, 31, 82, 253, 92),
                ("Austin", "Reaves", 15, 2, 26, 77, 197, 78),
                ("D'Angelo", "Russell", 1, 1, 28, 77, 193, 79),
                ("Rui", "Hachimura", 28, 4, 26, 80, 230, 76),
                ("Taurean", "Prince", 12, 3, 30, 79, 218, 73),
                ("Jaxson", "Hayes", 11, 5, 24, 83, 220, 72),
                ("Max", "Christie", 10, 2, 21, 78, 190, 71)
            }, rng));
            
            // Memphis Grizzlies
            players.AddRange(GenerateTeamRoster("MEM", new[] {
                ("Ja", "Morant", 12, 1, 25, 75, 174, 92),
                ("Desmond", "Bane", 22, 2, 26, 77, 215, 82),
                ("Marcus", "Smart", 36, 1, 30, 75, 220, 78),
                ("Jaren", "Jackson Jr.", 13, 4, 24, 83, 242, 83),
                ("Steven", "Adams", 4, 5, 31, 84, 265, 75),
                ("Santi", "Aldama", 7, 4, 23, 83, 220, 74),
                ("Luke", "Kennard", 10, 2, 28, 77, 206, 73),
                ("GG", "Jackson", 45, 4, 19, 81, 205, 72)
            }, rng));
            
            // Minnesota Timberwolves
            players.AddRange(GenerateTeamRoster("MIN", new[] {
                ("Anthony", "Edwards", 5, 2, 23, 76, 225, 91),
                ("Karl-Anthony", "Towns", 32, 5, 28, 84, 248, 86),
                ("Rudy", "Gobert", 27, 5, 32, 85, 258, 84),
                ("Jaden", "McDaniels", 3, 3, 24, 81, 185, 78),
                ("Mike", "Conley", 10, 1, 36, 73, 175, 76),
                ("Nickeil", "Alexander-Walker", 0, 1, 25, 77, 205, 73),
                ("Naz", "Reid", 11, 5, 25, 81, 264, 75),
                ("Kyle", "Anderson", 5, 4, 31, 81, 230, 72)
            }, rng));
            
            // New Orleans Pelicans
            players.AddRange(GenerateTeamRoster("NOP", new[] {
                ("Zion", "Williamson", 1, 4, 24, 78, 284, 90),
                ("Brandon", "Ingram", 14, 3, 27, 81, 190, 85),
                ("CJ", "McCollum", 3, 2, 33, 75, 190, 81),
                ("Herb", "Jones", 5, 3, 26, 80, 209, 78),
                ("Jonas", "Valanciunas", 17, 5, 32, 84, 265, 77),
                ("Trey", "Murphy III", 25, 3, 24, 81, 206, 76),
                ("Jose", "Alvarado", 15, 1, 26, 72, 179, 73),
                ("Dyson", "Daniels", 11, 1, 21, 79, 196, 73)
            }, rng));
            
            // Oklahoma City Thunder
            players.AddRange(GenerateTeamRoster("OKC", new[] {
                ("Shai", "Gilgeous-Alexander", 2, 1, 26, 78, 195, 93),
                ("Jalen", "Williams", 8, 3, 23, 78, 209, 83),
                ("Chet", "Holmgren", 7, 5, 22, 85, 208, 84),
                ("Josh", "Giddey", 3, 1, 21, 80, 205, 78),
                ("Luguentz", "Dort", 5, 2, 25, 76, 215, 76),
                ("Isaiah", "Joe", 11, 2, 25, 76, 180, 73),
                ("Cason", "Wallace", 22, 1, 20, 76, 193, 73),
                ("Kenrich", "Williams", 34, 4, 29, 79, 210, 72)
            }, rng));
            
            // Phoenix Suns
            players.AddRange(GenerateTeamRoster("PHX", new[] {
                ("Kevin", "Durant", 35, 3, 36, 83, 240, 93),
                ("Devin", "Booker", 1, 2, 28, 78, 206, 90),
                ("Bradley", "Beal", 3, 2, 31, 76, 207, 83),
                ("Jusuf", "Nurkic", 20, 5, 30, 84, 290, 77),
                ("Grayson", "Allen", 12, 2, 29, 76, 198, 76),
                ("Eric", "Gordon", 23, 2, 36, 76, 215, 73),
                ("Royce", "O'Neale", 00, 3, 31, 78, 226, 72),
                ("Bol", "Bol", 11, 5, 24, 86, 220, 71)
            }, rng));
            
            // Portland Trail Blazers
            players.AddRange(GenerateTeamRoster("POR", new[] {
                ("Anfernee", "Simons", 1, 2, 25, 75, 181, 82),
                ("Scoot", "Henderson", 0, 1, 20, 74, 195, 79),
                ("Shaedon", "Sharpe", 17, 2, 21, 78, 200, 77),
                ("Jerami", "Grant", 9, 4, 30, 80, 210, 78),
                ("Deandre", "Ayton", 2, 5, 26, 83, 250, 79),
                ("Matisse", "Thybulle", 4, 3, 27, 77, 201, 72),
                ("Malcolm", "Brogdon", 11, 1, 31, 77, 229, 76),
                ("Toumani", "Camara", 33, 4, 23, 80, 220, 71)
            }, rng));
            
            // Sacramento Kings
            players.AddRange(GenerateTeamRoster("SAC", new[] {
                ("De'Aaron", "Fox", 5, 1, 26, 75, 185, 88),
                ("Domantas", "Sabonis", 10, 5, 28, 83, 240, 86),
                ("Keegan", "Murray", 13, 3, 24, 80, 215, 80),
                ("Malik", "Monk", 0, 2, 26, 75, 200, 77),
                ("Harrison", "Barnes", 40, 3, 32, 80, 225, 76),
                ("Kevin", "Huerter", 9, 2, 26, 79, 190, 75),
                ("Trey", "Lyles", 41, 4, 28, 82, 234, 72),
                ("Davion", "Mitchell", 15, 1, 26, 74, 202, 74)
            }, rng));
            
            // San Antonio Spurs
            players.AddRange(GenerateTeamRoster("SAS", new[] {
                ("Victor", "Wembanyama", 1, 5, 20, 88, 210, 88), // Generational
                ("Devin", "Vassell", 24, 2, 24, 78, 194, 79),
                ("Keldon", "Johnson", 3, 3, 24, 77, 220, 76),
                ("Jeremy", "Sochan", 10, 4, 21, 81, 230, 76),
                ("Tre", "Jones", 33, 1, 24, 73, 185, 73),
                ("Doug", "McDermott", 17, 3, 32, 80, 225, 72),
                ("Zach", "Collins", 23, 5, 27, 84, 250, 73),
                ("Cedi", "Osman", 8, 3, 29, 80, 230, 70)
            }, rng));
            
            // Utah Jazz
            players.AddRange(GenerateTeamRoster("UTA", new[] {
                ("Lauri", "Markkanen", 23, 4, 27, 84, 240, 84),
                ("Collin", "Sexton", 2, 1, 25, 73, 190, 77),
                ("Jordan", "Clarkson", 00, 2, 32, 76, 194, 77),
                ("John", "Collins", 20, 4, 26, 81, 235, 78),
                ("Walker", "Kessler", 24, 5, 23, 85, 245, 76),
                ("Keyonte", "George", 3, 1, 20, 76, 185, 73),
                ("Talen", "Horton-Tucker", 0, 2, 24, 76, 234, 71),
                ("Kelly", "Olynyk", 41, 5, 33, 84, 240, 73)
            }, rng));
            
            return players;
        }
        
        private static List<PlayerData> GenerateTeamRoster(string teamId, 
            (string first, string last, int jersey, int pos, int age, int height, int weight, int overall)[] roster,
            System.Random rng)
        {
            var players = new List<PlayerData>();
            
            foreach (var p in roster)
            {
                players.Add(GeneratePlayer(teamId, p.first, p.last, p.jersey, p.pos, p.age, p.height, p.weight, p.overall, rng));
            }
            
            return players;
        }
        
        private static PlayerData GeneratePlayer(string teamId, string first, string last, int jersey, 
            int pos, int age, int height, int weight, int overall, System.Random rng)
        {
            // Generate stats based on overall and position
            int variance(int val) => val + rng.Next(-5, 6);
            int posBonus(int forPos, int bonus) => pos == forPos ? bonus : 0;
            
            return new PlayerData
            {
                PlayerId = $"{teamId}_{last.ToUpper()}_{first.ToUpper()}".Replace(" ", "_").Replace("'", "").Replace(".", ""),
                FirstName = first,
                LastName = last,
                JerseyNumber = jersey,
                Position = pos,
                Age = age,
                HeightInches = height,
                WeightLbs = weight,
                TeamId = teamId,
                
                // Offense
                Finishing_Rim = variance(overall - 5 + posBonus(5, 10) + posBonus(4, 5)),
                Finishing_PostMoves = variance(overall - 20 + posBonus(5, 25) + posBonus(4, 15)),
                Shot_Close = variance(overall - 3),
                Shot_MidRange = variance(overall - 2 + posBonus(2, 5)),
                Shot_Three = variance(overall - 5 + posBonus(2, 8) + posBonus(1, 5)),
                FreeThrow = variance(overall + posBonus(1, 8) + posBonus(2, 5)),
                Passing = variance(overall - 5 + posBonus(1, 15)),
                BallHandling = variance(overall - 5 + posBonus(1, 15) + posBonus(2, 8)),
                OffensiveIQ = variance(overall),
                SpeedWithBall = variance(overall - 5 + posBonus(1, 10)),
                
                // Defense
                Defense_Perimeter = variance(overall - 8 + posBonus(1, 5) + posBonus(2, 5)),
                Defense_Interior = variance(overall - 15 + posBonus(5, 20) + posBonus(4, 10)),
                Defense_PostDefense = variance(overall - 15 + posBonus(5, 20) + posBonus(4, 10)),
                Steal = variance(overall - 15 + posBonus(1, 10)),
                Block = variance(overall - 30 + posBonus(5, 35) + posBonus(4, 20)),
                DefensiveIQ = variance(overall - 5),
                DefensiveRebound = variance(overall - 20 + posBonus(5, 30) + posBonus(4, 20)),
                
                // Physical
                Speed = variance(overall - 5 + posBonus(1, 10) - posBonus(5, 10)),
                Acceleration = variance(overall - 3 + posBonus(1, 8) - posBonus(5, 8)),
                Strength = variance(overall - 15 + posBonus(5, 20) + posBonus(4, 15)),
                Vertical = variance(overall - 10 + posBonus(2, 8)),
                Stamina = variance(overall - 5),
                Durability = variance(overall - 10),
                Wingspan = (height / 12), // Simplified
                
                // Intangibles
                BasketballIQ = variance(overall),
                Clutch = variance(overall - 5),
                Consistency = variance(overall - 5),
                WorkEthic = variance(overall - 5),
                Coachability = variance(80),
                Ego = variance(50 + (overall > 85 ? 20 : 0)),
                Leadership = variance(overall - 15 + (age > 28 ? 15 : 0)),
                Composure = variance(overall - 5),
                Aggression = variance(50),
                
                // Status
                Energy = 100,
                Morale = 75,
                Form = 70
            };
        }
    }
    
    [Serializable]
    public class PlayerData
    {
        public string PlayerId, FirstName, LastName, TeamId;
        public int JerseyNumber, Position, Age, HeightInches, WeightLbs;
        
        // Offense
        public int Finishing_Rim, Finishing_PostMoves, Shot_Close, Shot_MidRange, Shot_Three;
        public int FreeThrow, Passing, BallHandling, OffensiveIQ, SpeedWithBall;
        
        // Defense  
        public int Defense_Perimeter, Defense_Interior, Defense_PostDefense;
        public int Steal, Block, DefensiveIQ, DefensiveRebound;
        
        // Physical
        public int Speed, Acceleration, Strength, Vertical, Stamina, Durability, Wingspan;
        
        // Intangibles
        public int BasketballIQ, Clutch, Consistency, WorkEthic, Coachability;
        public int Ego, Leadership, Composure, Aggression;
        
        // Status
        public int Energy, Morale, Form;
        
        public int OverallRating => (Shot_MidRange + Shot_Three + Passing + Defense_Perimeter + BasketballIQ) / 5;
    }
}
