using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using OpenAI;

public class GPTUtilities : MonoBehaviour {
    public VectorStoreApiClient vectorStoreAPI;
    public OpenAIConfiguration openAIConfiguration;

    private void Awake() {
        vectorStoreAPI = new VectorStoreApiClient();
        openAIConfiguration = Resources.Load<OpenAIConfiguration>("OpenAIConfiguration");
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration asset not found in Resources.");
        }
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            Task.Run(async () => await ListVectorStores());
        }
    }

    public async Task ListVectorStores() {
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration is not loaded.");
            return;
        }

        string apiKey = openAIConfiguration.ApiKey;

        try {
            var vectorStoresResponse = await vectorStoreAPI.GetVectorStoresAsync(apiKey);
            // Check if the response contains data
            if (vectorStoresResponse != null && vectorStoresResponse.Data != null) {
                Debug.Log("Vector Stores:");
                // Iterate over the list of vector stores
                foreach (var vectorStore in vectorStoresResponse.Data) {
                    Debug.Log($"ID: {vectorStore.Id}, Name: {vectorStore.Name}, Created At: {vectorStore.CreatedAt}");
                }
            } else {
                Debug.Log("No vector stores found.");
            }
        } catch (Exception e) {
            Debug.LogError($"Request error: {e.Message}");
        }
    }
}

public class VectorStoreApiClient {
    private static readonly HttpClient client = new HttpClient();

    static VectorStoreApiClient() {
        client.BaseAddress = new Uri("https://api.openai.com/");
    }

    private static void ConfigureClient(string apiKey) {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
    }

    public async Task<VectorStoreListResponse> GetVectorStoresAsync(string apiKey) {
        ConfigureClient(apiKey);
        var response = await client.GetAsync("v1/vector_stores");

        if (response.IsSuccessStatusCode) {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<VectorStoreListResponse>(jsonResponse);
        } else {
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
        }
    }

    public async Task<string> CreateVectorStoreFileAsync(string apiKey, string vectorStoreId, string fileId) {
        ConfigureClient(apiKey);
        var jsonContent = new StringContent($"{{\"file_id\": \"{fileId}\"}}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"v1/vector_stores/{vectorStoreId}/files", jsonContent);

        if (response.IsSuccessStatusCode) {
            return await response.Content.ReadAsStringAsync();
        } else {
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
        }
    }
}

public class VectorStoreListResponse {
    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("data")]
    public List<VectorStoreData> Data { get; set; }

    [JsonProperty("first_id")]
    public string FirstId { get; set; }

    [JsonProperty("last_id")]
    public string LastId { get; set; }

    [JsonProperty("has_more")]
    public bool HasMore { get; set; }
}

public class VectorStoreData {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("bytes")]
    public int Bytes { get; set; }

    [JsonProperty("file_counts")]
    public FileCounts FileCounts { get; set; }
}

public class FileCounts {
    [JsonProperty("in_progress")]
    public int InProgress { get; set; }

    [JsonProperty("completed")]
    public int Completed { get; set; }

    [JsonProperty("failed")]
    public int Failed { get; set; }

    [JsonProperty("cancelled")]
    public int Cancelled { get; set; }

    [JsonProperty("total")]
    public int Total { get; set; }
}
