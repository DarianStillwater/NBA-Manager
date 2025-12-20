using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Util
{
    /// <summary>
    /// Procedural name generator using Markov chains.
    /// Generates culturally-accurate names based on NBA demographics.
    /// </summary>
    public static class NameGenerator
    {
        // Markov models by nationality
        private static Dictionary<Nationality, MarkovChain> _firstNameModels;
        private static Dictionary<Nationality, MarkovChain> _lastNameModels;

        // Track used names for distinctness
        private static HashSet<string> _usedPlayerNames = new HashSet<string>();
        private static HashSet<string> _usedStaffNames = new HashSet<string>();

        // Nationality weights based on NBA demographics
        private static readonly Dictionary<Nationality, float> _nationalityWeights = new Dictionary<Nationality, float>
        {
            { Nationality.AmericanGeneral, 0.50f },
            { Nationality.AfricanAmerican, 0.25f },
            { Nationality.European_French, 0.03f },
            { Nationality.European_Spanish, 0.02f },
            { Nationality.European_Serbian, 0.02f },
            { Nationality.European_German, 0.02f },
            { Nationality.European_Greek, 0.01f },
            { Nationality.LatinAmerican, 0.08f },
            { Nationality.African_Nigerian, 0.03f },
            { Nationality.African_Cameroonian, 0.01f },
            { Nationality.Australian, 0.02f },
            { Nationality.Canadian, 0.01f }
        };

        private static bool _initialized = false;
        private static System.Random _rng = new System.Random();

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Generate a player name with optional nationality preference.
        /// </summary>
        public static GeneratedName GeneratePlayerName(Nationality nationality = Nationality.Random)
        {
            EnsureInitialized();

            if (nationality == Nationality.Random)
                nationality = PickRandomNationality();

            var name = GenerateNameInternal(nationality);

            // Try to avoid duplicates with staff (up to 5 attempts)
            int attempts = 0;
            while (_usedStaffNames.Contains(name.FullName) && attempts < 5)
            {
                name = GenerateNameInternal(nationality);
                attempts++;
            }

            _usedPlayerNames.Add(name.FullName);
            return name;
        }

        /// <summary>
        /// Generate a staff name with optional nationality preference.
        /// </summary>
        public static GeneratedName GenerateStaffName(Nationality nationality = Nationality.Random)
        {
            EnsureInitialized();

            if (nationality == Nationality.Random)
                nationality = PickRandomNationality();

            var name = GenerateNameInternal(nationality);

            // Try to avoid duplicates with players (up to 5 attempts)
            int attempts = 0;
            while (_usedPlayerNames.Contains(name.FullName) && attempts < 5)
            {
                name = GenerateNameInternal(nationality);
                attempts++;
            }

            _usedStaffNames.Add(name.FullName);
            return name;
        }

        /// <summary>
        /// Clear all tracked names (call when starting new game).
        /// </summary>
        public static void ClearTrackedNames()
        {
            _usedPlayerNames.Clear();
            _usedStaffNames.Clear();
        }

        /// <summary>
        /// Force reinitialization of Markov models.
        /// </summary>
        public static void Reinitialize()
        {
            _initialized = false;
            EnsureInitialized();
        }

        // ==================== INTERNAL METHODS ====================

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            _firstNameModels = new Dictionary<Nationality, MarkovChain>();
            _lastNameModels = new Dictionary<Nationality, MarkovChain>();

            // Initialize models for each nationality
            foreach (var nationality in _nationalityWeights.Keys)
            {
                var firstNames = GetSeedNames(nationality, true);
                var lastNames = GetSeedNames(nationality, false);

                var firstModel = new MarkovChain(2);
                firstModel.Train(firstNames);
                _firstNameModels[nationality] = firstModel;

                var lastModel = new MarkovChain(2);
                lastModel.Train(lastNames);
                _lastNameModels[nationality] = lastModel;
            }

            _initialized = true;
            Debug.Log("[NameGenerator] Initialized with Markov models for all nationalities");
        }

        private static GeneratedName GenerateNameInternal(Nationality nationality)
        {
            if (!_firstNameModels.ContainsKey(nationality))
                nationality = Nationality.AmericanGeneral;

            string firstName = _firstNameModels[nationality].Generate(3, 10);
            string lastName = _lastNameModels[nationality].Generate(3, 12);

            // Capitalize properly
            firstName = CapitalizeName(firstName);
            lastName = CapitalizeName(lastName);

            return new GeneratedName
            {
                FirstName = firstName,
                LastName = lastName,
                Nationality = nationality
            };
        }

        private static Nationality PickRandomNationality()
        {
            float roll = (float)_rng.NextDouble();
            float cumulative = 0f;

            foreach (var kvp in _nationalityWeights)
            {
                cumulative += kvp.Value;
                if (roll <= cumulative)
                    return kvp.Key;
            }

            return Nationality.AmericanGeneral;
        }

        private static string CapitalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpper(name[0]) + name.Substring(1).ToLower();
        }

        // ==================== SEED DATA ====================

        private static List<string> GetSeedNames(Nationality nationality, bool isFirstName)
        {
            // Return curated seed names for Markov training
            return nationality switch
            {
                Nationality.AmericanGeneral => isFirstName ? AmericanFirstNames : AmericanLastNames,
                Nationality.AfricanAmerican => isFirstName ? AfricanAmericanFirstNames : AfricanAmericanLastNames,
                Nationality.European_French => isFirstName ? FrenchFirstNames : FrenchLastNames,
                Nationality.European_Spanish => isFirstName ? SpanishFirstNames : SpanishLastNames,
                Nationality.European_Serbian => isFirstName ? SerbianFirstNames : SerbianLastNames,
                Nationality.European_German => isFirstName ? GermanFirstNames : GermanLastNames,
                Nationality.European_Greek => isFirstName ? GreekFirstNames : GreekLastNames,
                Nationality.LatinAmerican => isFirstName ? LatinFirstNames : LatinLastNames,
                Nationality.African_Nigerian => isFirstName ? NigerianFirstNames : NigerianLastNames,
                Nationality.African_Cameroonian => isFirstName ? CameroonianFirstNames : CameroonianLastNames,
                Nationality.Australian => isFirstName ? AustralianFirstNames : AustralianLastNames,
                Nationality.Canadian => isFirstName ? CanadianFirstNames : CanadianLastNames,
                _ => isFirstName ? AmericanFirstNames : AmericanLastNames
            };
        }

        // American General Names
        private static readonly List<string> AmericanFirstNames = new List<string>
        {
            "James", "Michael", "Robert", "John", "David", "William", "Richard", "Joseph", "Thomas", "Christopher",
            "Charles", "Daniel", "Matthew", "Anthony", "Mark", "Donald", "Steven", "Paul", "Andrew", "Joshua",
            "Kenneth", "Kevin", "Brian", "George", "Timothy", "Ronald", "Edward", "Jason", "Jeffrey", "Ryan",
            "Jacob", "Gary", "Nicholas", "Eric", "Jonathan", "Stephen", "Larry", "Justin", "Scott", "Brandon",
            "Benjamin", "Samuel", "Raymond", "Gregory", "Frank", "Alexander", "Patrick", "Jack", "Dennis", "Jerry"
        };

        private static readonly List<string> AmericanLastNames = new List<string>
        {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
            "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
            "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
            "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts"
        };

        // African-American Names
        private static readonly List<string> AfricanAmericanFirstNames = new List<string>
        {
            "DeAndre", "DeMarcus", "LeBron", "Dwyane", "Jamal", "Terrell", "Tyrone", "Darnell", "Rasheed", "Shaquille",
            "Kobe", "Lamar", "Malik", "Jaylen", "Jalen", "Darius", "Marcus", "Quincy", "Reggie", "Terrance",
            "Antoine", "Andre", "Cornell", "Deon", "Donovan", "Isaiah", "Jabari", "Kareem", "Kendrick", "Kyrie",
            "Marquis", "Marvin", "Montrell", "Rashad", "Rodney", "Shaun", "Trevon", "Tyrell", "Wendell", "Xavier",
            "Zion", "Dejuan", "Devonte", "Immanuel", "Jaren", "Kawhi", "Lonzo", "Myles", "Nassir", "Obi"
        };

        private static readonly List<string> AfricanAmericanLastNames = new List<string>
        {
            "Washington", "Jefferson", "Jackson", "Robinson", "Williams", "Johnson", "Brown", "Davis", "Jones", "Wilson",
            "Thomas", "Moore", "Taylor", "Harris", "Martin", "Thompson", "White", "Lewis", "Walker", "Green",
            "King", "Scott", "Young", "Mitchell", "Carter", "Turner", "Parker", "Collins", "Edwards", "Stewart",
            "Morris", "Murphy", "Cook", "Rogers", "Morgan", "Peterson", "Cooper", "Reed", "Bailey", "Bell",
            "Howard", "Ward", "Watson", "Brooks", "Sanders", "Price", "Bennett", "Henderson", "Coleman", "Powell"
        };

        // French Names
        private static readonly List<string> FrenchFirstNames = new List<string>
        {
            "Nicolas", "Antoine", "Rudy", "Evan", "Tony", "Boris", "Joakim", "Nando", "Rodrigue", "Timothe",
            "Frank", "Vincent", "Alexis", "Hugo", "Theo", "Louis", "Guillaume", "Julien", "Axel", "Killian",
            "Victor", "Olivier", "Elie", "Guerschon", "Petr", "Joel", "Ian", "Sekou", "Jayson", "Charles"
        };

        private static readonly List<string> FrenchLastNames = new List<string>
        {
            "Batum", "Gobert", "Parker", "Diaw", "Fournier", "Ntilikina", "Luwawu", "Poirier", "Kabengele", "Maledon",
            "Martin", "Durand", "Dubois", "Moreau", "Laurent", "Simon", "Michel", "Leroy", "Roux", "David",
            "Lefebvre", "Garcia", "Petit", "Bertrand", "Morel", "Girard", "Andre", "Mercier", "Dupont", "Lambert"
        };

        // Spanish Names
        private static readonly List<string> SpanishFirstNames = new List<string>
        {
            "Pau", "Marc", "Ricky", "Jose", "Juan", "Sergio", "Rudy", "Willy", "Juancho", "Santi",
            "Carlos", "Fernando", "Alberto", "Alejandro", "Pablo", "Raul", "Victor", "Diego", "Jorge", "Alvaro"
        };

        private static readonly List<string> SpanishLastNames = new List<string>
        {
            "Gasol", "Rubio", "Fernandez", "Calderon", "Hernangomez", "Aldama", "Llull", "Navarro", "Rodriguez", "Lopez",
            "Martinez", "Sanchez", "Gonzalez", "Perez", "Garcia", "Diaz", "Torres", "Ruiz", "Ramirez", "Moreno"
        };

        // Serbian Names
        private static readonly List<string> SerbianFirstNames = new List<string>
        {
            "Nikola", "Boban", "Bogdan", "Nemanja", "Milos", "Aleksa", "Vladimir", "Vlade", "Predrag", "Peja",
            "Marko", "Stefan", "Darko", "Ognjen", "Vasilije", "Filip", "Uros", "Luka", "Dusan", "Aleksandar"
        };

        private static readonly List<string> SerbianLastNames = new List<string>
        {
            "Jokic", "Marjanovic", "Bogdanovic", "Bjelica", "Teodosic", "Stojakovic", "Divac", "Kuzmic", "Simonovic", "Avramovic",
            "Petrovic", "Nikolic", "Jovanovic", "Popovic", "Djordjevic", "Ivanovic", "Milicic", "Obradovic", "Radulovic", "Vukcevic"
        };

        // German Names
        private static readonly List<string> GermanFirstNames = new List<string>
        {
            "Dirk", "Dennis", "Detlef", "Uwe", "Christian", "Moritz", "Daniel", "Maxi", "Isaac", "Franz",
            "Klaus", "Wolfgang", "Hans", "Werner", "Ralf", "Stefan", "Thomas", "Martin", "Andreas", "Markus"
        };

        private static readonly List<string> GermanLastNames = new List<string>
        {
            "Nowitzki", "Schroder", "Schrempf", "Blab", "Theis", "Wagner", "Bonga", "Hartenstein", "Pleiss", "Zipser",
            "Muller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann"
        };

        // Greek Names
        private static readonly List<string> GreekFirstNames = new List<string>
        {
            "Giannis", "Kostas", "Thanasis", "Nick", "Georgios", "Vassilis", "Dimitris", "Nikos", "Konstantinos", "Ioannis",
            "Alexandros", "Christos", "Stefanos", "Panagiotis", "Evangelos", "Michalis", "Andreas", "Petros", "Spiros", "Athanasios"
        };

        private static readonly List<string> GreekLastNames = new List<string>
        {
            "Antetokounmpo", "Calathes", "Papanikolaou", "Spanoulis", "Diamantidis", "Papadopoulos", "Papagiannis", "Koufos", "Bourousis", "Printezis",
            "Georgiou", "Konstantinou", "Nikolaou", "Papadakis", "Christodoulou", "Vasileiou", "Ioannidis", "Alexiou", "Dimitriou", "Stavrou"
        };

        // Latin American Names
        private static readonly List<string> LatinFirstNames = new List<string>
        {
            "Carlos", "Luis", "Jose", "Juan", "Miguel", "Angel", "Emanuel", "Leandro", "Facundo", "Gabriel",
            "Manu", "Pablo", "Andres", "Diego", "Nicolas", "Fabricio", "Bruno", "Marcos", "Raul", "Victor"
        };

        private static readonly List<string> LatinLastNames = new List<string>
        {
            "Ginobili", "Scola", "Nocioni", "Campazzo", "Deck", "Bolmaro", "Garino", "Vildoza", "Laprovittola", "Delfino",
            "Sanchez", "Rodriguez", "Hernandez", "Lopez", "Martinez", "Gonzalez", "Perez", "Ramirez", "Torres", "Rivera"
        };

        // Nigerian Names
        private static readonly List<string> NigerianFirstNames = new List<string>
        {
            "Hakeem", "Festus", "Gorgui", "Bismack", "Al-Farouq", "Ekpe", "Ike", "Olumide", "Chimezie", "Udoka",
            "Chukwuemeka", "Obinna", "Chijioke", "Ikechukwu", "Nnamdi", "Oluwaseun", "Adebayo", "Olajuwon", "Emeka", "Nnanna"
        };

        private static readonly List<string> NigerianLastNames = new List<string>
        {
            "Olajuwon", "Ezeli", "Dieng", "Biyombo", "Aminu", "Udoh", "Diogu", "Oyedeji", "Metu", "Azubuike",
            "Okafor", "Onuaku", "Antetokounmpo", "Okonkwo", "Eze", "Nwankwo", "Adeyemi", "Afolabi", "Ogunleye", "Babatunde"
        };

        // Cameroonian Names
        private static readonly List<string> CameroonianFirstNames = new List<string>
        {
            "Joel", "Pascal", "Luc", "Ruben", "Bruno", "Christian", "Serge", "Cedric", "Landry", "Gaston",
            "Blaise", "Herve", "Francois", "Guy", "Pierre", "Andre", "Michel", "Charles", "Joseph", "Emmanuel"
        };

        private static readonly List<string> CameroonianLastNames = new List<string>
        {
            "Embiid", "Siakam", "Mbah", "Moute", "Biyombo", "Eboua", "Noubissi", "Ndour", "Faye", "Diallo",
            "Ndiaye", "Sow", "Ba", "Traore", "Diop", "Camara", "Toure", "Kone", "Coulibaly", "Sanogo"
        };

        // Australian Names
        private static readonly List<string> AustralianFirstNames = new List<string>
        {
            "Ben", "Patty", "Joe", "Matthew", "Andrew", "Aron", "Josh", "Dante", "Ryan", "Thon",
            "Jack", "Liam", "Oliver", "Noah", "William", "James", "Lucas", "Henry", "Alexander", "Max"
        };

        private static readonly List<string> AustralianLastNames = new List<string>
        {
            "Simmons", "Mills", "Ingles", "Dellavedova", "Bogut", "Baynes", "Giddey", "Exum", "Broekhoff", "Maker",
            "Smith", "Jones", "Brown", "Wilson", "Taylor", "Anderson", "Thompson", "White", "Martin", "Walker"
        };

        // Canadian Names
        private static readonly List<string> CanadianFirstNames = new List<string>
        {
            "Andrew", "Steve", "Jamal", "Tristan", "Kelly", "Cory", "Nik", "Dwight", "Shai", "RJ",
            "Luguentz", "Dillon", "Chris", "Brandon", "Bennedict", "Nickeil", "Trey", "Oshae", "Nate", "Khem"
        };

        private static readonly List<string> CanadianLastNames = new List<string>
        {
            "Wiggins", "Nash", "Murray", "Thompson", "Olynyk", "Joseph", "Stauskas", "Powell", "Gilgeous-Alexander", "Barrett",
            "Dort", "Brooks", "Boucher", "Alexander", "Mathurin", "Lyles", "Brissett", "Birch", "Nembhard", "Lawson"
        };
    }

    // ==================== SUPPORTING CLASSES ====================

    /// <summary>
    /// Generated name result with metadata.
    /// </summary>
    public class GeneratedName
    {
        public string FirstName;
        public string LastName;
        public Nationality Nationality;

        public string FullName => $"{FirstName} {LastName}";

        public override string ToString() => FullName;
    }

    /// <summary>
    /// Nationality options for name generation.
    /// </summary>
    public enum Nationality
    {
        Random,
        AmericanGeneral,
        AfricanAmerican,
        European_French,
        European_Spanish,
        European_Serbian,
        European_German,
        European_Greek,
        LatinAmerican,
        African_Nigerian,
        African_Cameroonian,
        Australian,
        Canadian
    }

    /// <summary>
    /// Character-level Markov chain for name generation.
    /// </summary>
    public class MarkovChain
    {
        private Dictionary<string, List<char>> _transitions = new Dictionary<string, List<char>>();
        private List<string> _starters = new List<string>();
        private int _order;
        private System.Random _rng = new System.Random();

        public MarkovChain(int order = 2)
        {
            _order = order;
        }

        /// <summary>
        /// Train the model on a list of names.
        /// </summary>
        public void Train(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name) || name.Length < _order)
                    continue;

                string lower = name.ToLower();

                // Record starter
                _starters.Add(lower.Substring(0, _order));

                // Build transitions
                for (int i = 0; i <= lower.Length - _order; i++)
                {
                    string key = lower.Substring(i, _order);
                    char next = (i + _order < lower.Length) ? lower[i + _order] : '\0';

                    if (!_transitions.ContainsKey(key))
                        _transitions[key] = new List<char>();

                    _transitions[key].Add(next);
                }
            }
        }

        /// <summary>
        /// Generate a name using the trained model.
        /// </summary>
        public string Generate(int minLength = 3, int maxLength = 12)
        {
            if (_starters.Count == 0)
                return "Unknown";

            int attempts = 0;
            while (attempts < 20)
            {
                attempts++;

                // Pick a random starter
                string result = _starters[_rng.Next(_starters.Count)];

                // Build the name
                while (result.Length < maxLength)
                {
                    string key = result.Substring(result.Length - _order, _order);

                    if (!_transitions.ContainsKey(key))
                        break;

                    var options = _transitions[key];
                    char next = options[_rng.Next(options.Count)];

                    if (next == '\0')
                        break;

                    result += next;
                }

                if (result.Length >= minLength)
                    return result;
            }

            // Fallback: return a random seed name
            return _starters[_rng.Next(_starters.Count)];
        }
    }
}
