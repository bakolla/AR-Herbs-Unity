using System;
using System.Collections.Generic;

namespace ARHerb.Data
{
    /// <summary>
    /// Root response structure returned by the backend's /api/identify endpoint.
    /// </summary>
    [Serializable]
    public class ScanResult
    {
        // Indicates overall success of the request
        public bool success;
        
        // Mode used (plants, mushrooms, insects, stones)
        public string mode;
        
        // Used in mushrooms, insects, stones mode (Gemini direct normalized format)
        public EnrichmentData enrichment;
        
        // Language code returned by backend
        public string language;
        
        // Best matching scientific name
        public string bestMatch;

        // List of candidate results (used in plants, mushrooms, insects, stones)
        public List<CandidateResult> results;
    }

    [Serializable]
    public class CandidateResult
    {
        // Confidence score between 0.0 and 1.0
        public float score;
        
        // Species details
        public SpeciesData species;
    }

    [Serializable]
    public class SpeciesData
    {
        public string scientificNameWithoutAuthor;
        public string scientificName;
        public List<string> commonNames;
        public FamilyData family;
    }

    [Serializable]
    public class FamilyData
    {
        public string scientificNameWithoutAuthor;
        public string scientificName;
    }

    /// <summary>
    /// Enrichment details containing edibility, description, and fun facts.
    /// </summary>
    [Serializable]
    public class EnrichmentData
    {
        public string edibleStatus; // "edible" | "toxic" | "both" | "unknown"
        public string edibleNote;
        public string funFact;
        public string description;
    }

    /// <summary>
    /// Response payload returned by /api/enrich endpoint.
    /// </summary>
    [Serializable]
    public class EnrichResponse
    {
        public EnrichmentData enrichment;
        public string error;
    }
}
