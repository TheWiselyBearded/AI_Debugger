using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using OpenAI;
using System.Linq;
using OpenAI.Files;

public class GPTUtilities {
    public OpenAIConfiguration openAIConfiguration;
    private string vectorStoreId = "";
    private HttpClient httpClient;
    private OpenAIClient api;
    public GPTUtilities() {
        httpClient = new HttpClient();
        api = new OpenAIClient();
    }

    public void Init() {
        openAIConfiguration = Resources.Load<OpenAIConfiguration>("OpenAIConfiguration");
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration asset not found in Resources.");
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync() {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Add("Authorization", $"Bearer {openAIConfiguration.ApiKey}");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonConvert.DeserializeObject<OpenAIModelsResponse>(responseBody);

        return jsonResponse.data.Select(model => model.id).ToList();
    }


    public async Task ListVectorStores() {
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration is not loaded.");
            return;
        }

        try {
            var vectorStoresResponse = await api.VectorStoresEndpoint.ListVectorStoresAsync();
            // Check if the response contains data
            if (vectorStoresResponse != null && vectorStoresResponse.Items != null) {
                Debug.Log("Vector Stores:");
                // Iterate over the list of vector stores
                foreach (var vectorStore in vectorStoresResponse.Items) {
                    Debug.Log($"ID: {vectorStore.Id}, Name: {vectorStore.Name}, Created At: {vectorStore.CreatedAt}");
                }
            } else {
                Debug.Log("No vector stores found.");
            }
        } catch (Exception e) {
            Debug.LogError($"Request error: {e.Message}");
        }
    }

    public async Task ListVectorStoreFiles(string vectorStoreId) {
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration is not loaded.");
            return;
        }

        try {
            var vectorStoreFilesResponse = await api.VectorStoresEndpoint.ListVectorStoreFilesAsync(vectorStoreId);
            // Check if the response contains data
            if (vectorStoreFilesResponse != null && vectorStoreFilesResponse.Items != null) {
                Debug.Log("Vector Store Files:");
                // Iterate over the list of vector store files
                foreach (var file in vectorStoreFilesResponse.Items) {
                    Debug.Log($"ID: {file.Id}, Created At: {file.CreatedAt}, Vector Store ID: {file.VectorStoreId}");
                }
                vectorStoreId = vectorStoreFilesResponse.Items[0].Id;
            } else {
                Debug.Log("No vector store files found.");
            }
        } catch (Exception e) {
            Debug.LogError($"Request error: {e.Message}");
        }
    }


    public async Task CreateAndUploadVectorStoreFile(string vectorStoreId, string filePath) {
        if (openAIConfiguration == null) {
            Debug.LogError("OpenAIConfiguration is not loaded.");
            return;
        }

        try {
            // Step 1: Upload the file and get the file_id
            var fileId = await api.FilesEndpoint.UploadFileAsync(filePath, FilePurpose.FineTune);
            Debug.Log($"File uploaded with ID: {fileId}");

            // Step 2: Use the file_id to create the vector store file
            var createFileResponse = await api.VectorStoresEndpoint.CreateVectorStoreFileAsync(vectorStoreId, fileId);
            Debug.Log("Vector Store File Created:");
            Debug.Log($"ID: {createFileResponse.Id}, Created At: {createFileResponse.CreatedAt}, Status: {createFileResponse.Status}, Vector Store ID: {createFileResponse.VectorStoreId}");
        } catch (Exception e) {
            Debug.LogError($"Request error: {e.Message}");
        }
    }


}

/*public class VectorStoreApiClient {
    private readonly HttpClient _httpClient;
    private string apiKey;


    public VectorStoreApiClient(string apiKey) {
        _httpClient = new HttpClient {
            BaseAddress = new Uri("https://api.openai.com/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
    }

    public async Task<VectorStoreListResponse> GetVectorStoresAsync() {
        var response = await _httpClient.GetAsync("v1/vector_stores");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<VectorStoreListResponse>(content);
    }

    

    public async Task<VectorStoreFileListResponse> ListVectorStoreFilesAsync(string vectorStoreId) {
        var response = await _httpClient.GetAsync($"v1/vector_stores/{vectorStoreId}/files");

        if (response.IsSuccessStatusCode) {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<VectorStoreFileListResponse>(jsonResponse);
        } else {
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
        }
    }



    public async Task<CreateVectorStoreFileResponse> CreateVectorStoreFileAsync(string vectorStoreId, string fileId) {
        var payload = new {
            file_id = fileId
        };

        var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"v1/vector_stores/{vectorStoreId}/files", jsonContent);

        if (response.IsSuccessStatusCode) {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CreateVectorStoreFileResponse>(jsonResponse);
        } else {
            var errorResponse = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}, {errorResponse}");
        }
    }



    public async Task<string> CreateFileAsync(string filePath) {
        var fileName = System.IO.Path.GetFileName(filePath);
        var fileContent = System.IO.File.ReadAllBytes(filePath);

        var payload = new {
            purpose = "assistants",
            file = Convert.ToBase64String(fileContent),
            file_id = fileName
        };

        var jsonContent = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/files", jsonContent);

        if (response.IsSuccessStatusCode) {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var fileResponse = JsonConvert.DeserializeObject<CreateFileResponse>(jsonResponse);
            return fileResponse.Id;
        } else {
            var errorResponse = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}, {errorResponse}");
        }
    }

    public async Task<string> UploadFileAsync(string filePath) {
        var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(filePath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
        form.Add(new StringContent("assistants"), "purpose");

        var response = await _httpClient.PostAsync("v1/files", form);

        if (response.IsSuccessStatusCode) {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var fileResponse = JsonConvert.DeserializeObject<CreateFileResponse>(jsonResponse);
            return fileResponse.Id;
        } else {
            var errorResponse = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request failed with status code: {response.StatusCode}, {errorResponse}");
        }
    }
}*/



public class OpenAIModelsResponse {
    [JsonProperty("data")]
    public List<OpenAIModel> data;
}

public class OpenAIModel {
    [JsonProperty("id")]
    public string id;

    [JsonProperty("created")]
    public string created;

    [JsonProperty("owned_by")]
    public string owned_by;
}

/*public class VectorStoreListResponse {
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

public class VectorStoreFileListResponse {
    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("data")]
    public List<VectorStoreFileData> Data { get; set; }

    [JsonProperty("first_id")]
    public string FirstId { get; set; }

    [JsonProperty("last_id")]
    public string LastId { get; set; }

    [JsonProperty("has_more")]
    public bool HasMore { get; set; }
}

public class VectorStoreFileData {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("vector_store_id")]
    public string VectorStoreId { get; set; }
}

public class CreateVectorStoreFileResponse {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("usage_bytes")]
    public int UsageBytes { get; set; }

    [JsonProperty("vector_store_id")]
    public string VectorStoreId { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("last_error")]
    public string LastError { get; set; }
}

public class CreateFileResponse {
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("object")]
    public string Object { get; set; }

    [JsonProperty("created_at")]
    public long CreatedAt { get; set; }

    [JsonProperty("bytes")]
    public int Bytes { get; set; }

    [JsonProperty("purpose")]
    public string Purpose { get; set; }

    [JsonProperty("filename")]
    public string Filename { get; set; }
}*/