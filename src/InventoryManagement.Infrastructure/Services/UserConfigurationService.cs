using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using InventoryManagement.Application.Services;

namespace InventoryManagement.Infrastructure.Services;

public interface IUserConfigurationService
{
    BarcodeScannerOptions LoadBarcodeScannerOptions(BarcodeScannerOptions defaults);
    void SaveBarcodeScannerOptions(BarcodeScannerOptions options);
}

public class UserConfigurationService : IUserConfigurationService
{
    private readonly string _configDir;
    private readonly string _configFile;

    public UserConfigurationService()
    {
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "InventoryManagement", "Config");
        _configFile = Path.Combine(_configDir, "user-settings.json");
    }

    public BarcodeScannerOptions LoadBarcodeScannerOptions(BarcodeScannerOptions defaults)
    {
        if (!File.Exists(_configFile))
        {
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configFile);
            var node = JsonNode.Parse(json);
            
            if (node?["BarcodeScanner"] != null)
            {
                var barcodeNode = node["BarcodeScanner"];
                
                var options = new BarcodeScannerOptions
                {
                    MinBarcodeLength = barcodeNode?["MinBarcodeLength"]?.GetValue<int>() ?? defaults.MinBarcodeLength,
                    MaxTimeBetweenKeystrokesMs = barcodeNode?["MaxTimeBetweenKeystrokesMs"]?.GetValue<int>() ?? defaults.MaxTimeBetweenKeystrokesMs,
                    RequireTerminatingEnter = barcodeNode?["RequireTerminatingEnter"]?.GetValue<bool>() ?? defaults.RequireTerminatingEnter
                };
                return options;
            }
        }
        catch 
        {
            // Log error in caller or ignore and fallback to defaults safely
        }
        
        return defaults;
    }

    public void SaveBarcodeScannerOptions(BarcodeScannerOptions options)
    {
        if (!Directory.Exists(_configDir))
        {
            Directory.CreateDirectory(_configDir);
        }

        JsonObject root;
        if (File.Exists(_configFile))
        {
            try
            {
                var json = File.ReadAllText(_configFile);
                root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        if (root["BarcodeScanner"] == null)
        {
            root["BarcodeScanner"] = new JsonObject();
        }

        root["BarcodeScanner"]!["MinBarcodeLength"] = options.MinBarcodeLength;
        root["BarcodeScanner"]!["MaxTimeBetweenKeystrokesMs"] = options.MaxTimeBetweenKeystrokesMs;
        root["BarcodeScanner"]!["RequireTerminatingEnter"] = options.RequireTerminatingEnter;

        File.WriteAllText(_configFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
