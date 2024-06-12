using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChatMessage {
    public string UserInput { get; private set; }
    public string Response { get; private set; }

    private static readonly System.Random random = new System.Random();

    public ChatMessage(string userInput) {
        UserInput = userInput;
    }

    public async Task SendToApiAsync() {
        // Simulate sending user input to an API and getting a response
        await Task.Delay(1000); // Simulate network delay
        Response = GenerateRandomString(10); // Simulated API response
    }

    private string GenerateRandomString(int length) {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        char[] stringChars = new char[length];
        for (int i = 0; i < length; i++) {
            stringChars[i] = chars[random.Next(chars.Length)];
        }
        return new string(stringChars);
    }
}