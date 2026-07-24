using System.Collections.Generic;

namespace ARHerb.Localization
{
    public static class LocalizationManager
    {
        public static string CurrentLanguage = "pl"; // "pl", "en", "el", "gr"

        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["pl"] = new Dictionary<string, string>
            {
                ["app_title"] = "🌿 HERB & FAUNA",
                ["btn_history"] = "📜 Historia",
                ["btn_scan"] = "SKANUJ",
                ["btn_wait"] = "CZEKAJ...",
                ["mode_auto"] = "Auto",
                ["mode_plants"] = "Rośliny",
                ["mode_mushrooms"] = "Grzyby",
                ["mode_insects"] = "Owady",
                ["mode_stones"] = "Kamienie",
                ["camera_select_title"] = "Wybierz Aparat",
                ["camera_select_btn"] = "Wybierz Aparat",
                ["no_gps"] = "Brak GPS",
                ["settings_title"] = "⚙️ Ustawienia",
                ["backend_url_label"] = "URL Backendu:",
                ["status_ready"] = "Gotowy do skanowania",
                ["status_capturing"] = "Przechwytywanie obrazu...",
                ["status_sending"] = "Wysyłanie do backendu...",
                ["status_analyzing"] = "Analizowanie z AI...",
                ["status_loading_details"] = "Ładowanie szczegółów...",
                ["status_done_return"] = "📷 Powrót do skanowania",
                ["status_camera_error"] = "Nie udało się pobrać obrazu z kamery.",
                ["status_no_match"] = "Nie znaleziono pasującego obiektu. Spróbuj bliżej i w lepszym świetle.",
                ["status_no_internet"] = "Brak połączenia z internetem.",
                ["status_backend_error"] = "Nie można połączyć się z backendem.",
                ["status_ai_error"] = "Nie udało się pobrać szczegółów AI.",
                ["status_cleared_history"] = "Wyczyszczono historię skanów.",
                ["score_label"] = "Prawdopodobieństwo",
                ["edibility_label"] = "Status spożywczy",
                ["edibility_no_data"] = "Brak danych",
                ["edibility_edible"] = "Jadalny / Bezpieczny",
                ["edibility_toxic"] = "Trujący / Niebezpieczny",
                ["edibility_both"] = "Częściowo jadalny / Warunkowo",
                ["edibility_unknown"] = "Nieznany / Brak danych",
                ["history_title"] = "📜 HISTORIA SKANOWANIA",
                ["history_clear_btn"] = "🗑️ WYCZYŚĆ HISTORIĘ",
                ["history_empty"] = "Brak zapisanych skanów w historii.\nWykonaj pierwsze skanowanie!",
                ["btn_open_maps"] = "🗺️ Otwórz w Mapach",
                ["btn_gallery"] = "🖼️ Galeria",
                ["location_no_data"] = "Brak danych GPS dla tego skanu."
            },
            ["en"] = new Dictionary<string, string>
            {
                ["app_title"] = "🌿 HERB & FAUNA",
                ["btn_history"] = "📜 History",
                ["btn_scan"] = "SCAN",
                ["btn_wait"] = "WAIT...",
                ["mode_auto"] = "Auto",
                ["mode_plants"] = "Plants",
                ["mode_mushrooms"] = "Mushrooms",
                ["mode_insects"] = "Insects",
                ["mode_stones"] = "Stones",
                ["camera_select_title"] = "Select Camera",
                ["camera_select_btn"] = "Select Camera",
                ["no_gps"] = "No GPS",
                ["settings_title"] = "⚙️ Settings",
                ["backend_url_label"] = "Backend URL:",
                ["status_ready"] = "Ready to scan",
                ["status_capturing"] = "Capturing image...",
                ["status_sending"] = "Sending to backend...",
                ["status_analyzing"] = "Analyzing with AI...",
                ["status_loading_details"] = "Loading details...",
                ["status_done_return"] = "📷 Return to scanning",
                ["status_camera_error"] = "Failed to capture camera image.",
                ["status_no_match"] = "No matching specimen found. Try closer and better light.",
                ["status_no_internet"] = "No internet connection.",
                ["status_backend_error"] = "Cannot connect to backend server.",
                ["status_ai_error"] = "Failed to fetch AI details.",
                ["status_cleared_history"] = "Scan history cleared.",
                ["score_label"] = "Confidence",
                ["edibility_label"] = "Edibility / Safety",
                ["edibility_no_data"] = "No data",
                ["edibility_edible"] = "Edible / Safe",
                ["edibility_toxic"] = "Toxic / Dangerous",
                ["edibility_both"] = "Conditionally Edible",
                ["edibility_unknown"] = "Unknown / Unverified",
                ["history_title"] = "📜 SCAN HISTORY",
                ["history_clear_btn"] = "🗑️ CLEAR HISTORY",
                ["history_empty"] = "No saved scans in history.\nPerform your first scan!",
                ["btn_open_maps"] = "🗺️ Open in Maps",
                ["btn_gallery"] = "🖼️ Gallery",
                ["location_no_data"] = "No GPS data for this scan."
            },
            ["el"] = new Dictionary<string, string>
            {
                ["app_title"] = "🌿 HERB & FAUNA",
                ["btn_history"] = "📜 Ιστορικό",
                ["btn_scan"] = "ΣΑΡΩΣΗ",
                ["btn_wait"] = "ΠΕΡΙΜΕΝΕ...",
                ["mode_auto"] = "Αυτόματο",
                ["mode_plants"] = "Φυτά",
                ["mode_mushrooms"] = "Μανιτάρια",
                ["mode_insects"] = "Έντομα",
                ["mode_stones"] = "Πέτρες",
                ["camera_select_title"] = "Επιλογή Κάμερας",
                ["camera_select_btn"] = "Επιλογή Κάμερας",
                ["no_gps"] = "Χωρίς GPS",
                ["settings_title"] = "⚙️ Ρυθμίσεις",
                ["backend_url_label"] = "URL Διακομιστή:",
                ["status_ready"] = "Έτοιμο για σάρωση",
                ["status_capturing"] = "Λήψη εικόνας...",
                ["status_sending"] = "Αποστολή στο backend...",
                ["status_analyzing"] = "Ανάλυση με AI...",
                ["status_loading_details"] = "Φόρτωση λεπτομερειών...",
                ["status_done_return"] = "📷 Επιστροφή στη σάρωση",
                ["status_camera_error"] = "Αποτυχία λήψης εικόνας κάμερας.",
                ["status_no_match"] = "Δεν βρέθηκε αντικείμενο. Δοκιμάστε πιο κοντά.",
                ["status_no_internet"] = "Χωρίς σύνδεση στο διαδίκτυο.",
                ["status_backend_error"] = "Αδυναμία σύνδεσης στο backend.",
                ["status_ai_error"] = "Αποτυχία λήψης λεπτομερειών AI.",
                ["status_cleared_history"] = "Το ιστορικό σαρώσεων καθαρίστηκε.",
                ["score_label"] = "Πιθανότητα",
                ["edibility_label"] = "Βρωσιμότητα / Ασφάλεια",
                ["edibility_no_data"] = "Χωρίς δεδομένα",
                ["edibility_edible"] = "Βρώσιμο / Ασφαλές",
                ["edibility_toxic"] = "Τοξικό / Επικίνδυνο",
                ["edibility_both"] = "Σχετικά Βρώσιμο",
                ["edibility_unknown"] = "Άγνωστα στοιχεία",
                ["history_title"] = "📜 ΙΣΤΟΡΙΚΟ ΣΑΡΩΣΕΩΝ",
                ["history_clear_btn"] = "🗑️ ΚΑΘΑΡΙΣΜΟΣ ΙΣΤΟΡΙΚΟΥ",
                ["history_empty"] = "Δεν υπάρχουν σαρώσεις στο ιστορικό.\nΚάντε την πρώτη σας σάρωση!",
                ["btn_open_maps"] = "🗺️ Άνοιγμα στους Χάρτες",
                ["btn_gallery"] = "🖼️ Γκαλερί",
                ["location_no_data"] = "Χωρίς δεδομένα GPS για αυτή τη σάρωση."
            }
        };

        public static string Get(string key)
        {
            string lang = string.IsNullOrEmpty(CurrentLanguage) ? "pl" : CurrentLanguage.ToLower();
            if (lang == "gr") lang = "el";
            if (!Translations.ContainsKey(lang)) lang = "pl";

            if (Translations[lang].TryGetValue(key, out string val))
            {
                return val;
            }

            if (Translations["pl"].TryGetValue(key, out string plVal))
            {
                return plVal;
            }

            return key;
        }
    }
}
