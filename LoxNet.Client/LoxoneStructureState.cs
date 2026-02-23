using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoxNet;

public class LoxoneStructureState : ILoxoneStructureState
{
    private readonly ILoxoneHttpClient _httpClient;
    private readonly ILogger<LoxoneStructureState> _logger;
    private readonly LoxoneConnectionOptions _options;
    private readonly Dictionary<string, LoxoneControl> _uuidMap = new();
    private readonly Dictionary<string, LoxoneRoom> _roomMap = new();
    private readonly Dictionary<string, LoxoneCategory> _categoryMap = new();
    private readonly Dictionary<int, string> _operatingModes = new();
    private readonly bool _lightMode;
    private ILoxoneWebSocketClient? _wsClient;

    /// <summary>
    /// Initializes the cache.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="httpClient">HTTP client for server communication.</param>
    /// <param name="options">Connection options including cache path.</param>
    /// <param name="lightMode">When <c>true</c> only <see cref="LoxoneControl"/> instances are created.</param>
    public LoxoneStructureState(ILogger<LoxoneStructureState> logger, ILoxoneHttpClient httpClient, LoxoneConnectionOptions options, bool lightMode = false, ILoxoneWebSocketClient? wsClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _lightMode = lightMode;
        if (wsClient != null)
        {
            AttachWebSocketClient(wsClient);
        }
    }

    /// <summary>
    /// Registers a WebSocket client so that state updates are applied to controls.
    /// </summary>
    /// <param name="client">Client used to receive update messages.</param>
    public void AttachWebSocketClient(ILoxoneWebSocketClient client)
    {
        _wsClient = client ?? throw new ArgumentNullException(nameof(client));
        _wsClient.MessageReceived += HandleWebSocketMessage;
    }

    public IReadOnlyDictionary<string, LoxoneControl> Controls => _uuidMap;
    public IReadOnlyDictionary<string, LoxoneRoom> Rooms => _roomMap;
    public IReadOnlyDictionary<string, LoxoneCategory> Categories => _categoryMap;
    public IReadOnlyDictionary<int, string> GetOperatingModes() => _operatingModes;

    public async Task LoadAsync(bool useCacheOnly = false, CancellationToken cancellationToken = default)
    {
        var cachePath = _options.StructureCachePath ?? Path.GetTempPath();
        var host = _options.Host;
        var serialNumber = string.Empty;

        // Step 1: Extract serial number from jdev/cfg/apiKey (required for cache filename)
        try
        {
            using var keyDoc = await _httpClient.RequestJsonAsync("jdev/cfg/apiKey", cancellationToken).ConfigureAwait(false);
            if (keyDoc.RootElement.TryGetProperty("LL", out var llProp) &&
                llProp.TryGetProperty("value", out var valueProp) &&
                valueProp.ValueKind == JsonValueKind.Object &&
                valueProp.TryGetProperty("macAddress", out var macProp) &&
                macProp.GetString() is { } mac)
            {
                // Extract last 6 chars of MAC address as serial (XX:XX:XX pattern -> XXXXXX)
                serialNumber = mac.Replace(":", "").Substring(Math.Max(0, mac.Replace(":", "").Length - 6));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[StructureLoad] Failed to extract serial number: {Error}. Using empty serial.", ex.Message);
        }

        var cacheFilename = $"{host}_{serialNumber}-LoxApp3";
        var cacheJsonPath = Path.Combine(cachePath, $"{cacheFilename}.json");
        var cacheMetaPath = Path.Combine(cachePath, $"{cacheFilename}.meta");
        var cacheLockPath = Path.Combine(cachePath, $"{cacheFilename}.lock");
        
        _logger.LogInformation("[StructureLoad] Using cache path: {CachePath}", cachePath);
        _logger.LogInformation("[StructureLoad] Cache files: json={CacheJsonPath} meta={CacheMetaPath}", Path.GetFileName(cacheJsonPath), Path.GetFileName(cacheMetaPath));

        // Step 2: Clean up legacy cache files (without serial pattern)
        try
        {
            var legacyPattern = $"{host}-LoxApp3";
            var legacyFiles = Directory.GetFiles(cachePath, $"{legacyPattern}*");
            foreach (var legacyFile in legacyFiles)
            {
                try
                {
                    File.Delete(legacyFile);
                    _logger.LogInformation("[StructureLoad] Deleted legacy cache file: {File}", Path.GetFileName(legacyFile));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[StructureLoad] Failed to delete legacy cache file {File}: {Error}", Path.GetFileName(legacyFile), ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[StructureLoad] Failed to clean legacy cache files: {Error}", ex.Message);
        }

        // Step 3: Acquire file lock
        FileStream? lockStream = null;
        try
        {
            lockStream = await FileLockHelper.AcquireLockAsync(cacheLockPath, _logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[StructureLoad] Failed to acquire cache lock: {Error}. Proceeding without lock.", ex.Message);
        }

        try
        {
            string jsonContent = string.Empty;

            // Step 4: Check cache freshness if cache exists
            var cacheStale = true;
            if (!useCacheOnly && File.Exists(cacheJsonPath) && File.Exists(cacheMetaPath))
            {
                try
                {
                    var metaJson = await File.ReadAllTextAsync(cacheMetaPath, cancellationToken).ConfigureAwait(false);
                    var metaOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var metadata = JsonSerializer.Deserialize<StructureCacheMetadata>(metaJson, metaOptions);
                    
                    if (metadata != null)
                    {
                        // Check cache freshness via jdev/sps/LoxAPPversion3
                        try
                        {
                            using var versionDoc = await _httpClient.RequestJsonAsync("jdev/sps/LoxAPPversion3", cancellationToken).ConfigureAwait(false);
                            if (versionDoc.RootElement.TryGetProperty("LL", out var llVersionProp) &&
                                llVersionProp.TryGetProperty("value", out var versionValueProp) &&
                                versionValueProp.GetString() is { } serverVersion)
                            {
                                cacheStale = serverVersion != metadata.LastModified;
                                if (!cacheStale)
                                {
                                    _logger.LogInformation("[StructureLoad] Cache is fresh (version: {Version}) from {CacheFile}", serverVersion, cacheJsonPath);
                                    jsonContent = await File.ReadAllTextAsync(cacheJsonPath, cancellationToken).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[StructureLoad] Failed to check cache freshness: {Error}. Assuming stale.", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[StructureLoad] Failed to read cache metadata: {Error}. Assuming stale.", ex.Message);
                }
            }

            // Step 5: Fetch from server if cache is stale or missing
            if (cacheStale && !useCacheOnly)
            {
                _logger.LogInformation("[StructureLoad] Fetching structure from server...");
                using (var doc = await _httpClient.RequestJsonAsync("data/LoxApp3.json", cancellationToken).ConfigureAwait(false))
                {
                    jsonContent = doc.RootElement.GetRawText();
                }

                // Extract version for metadata
                var lastModified = string.Empty;
                try
                {
                    using var versionDoc = await _httpClient.RequestJsonAsync("jdev/sps/LoxAPPversion3", cancellationToken).ConfigureAwait(false);
                    if (versionDoc.RootElement.TryGetProperty("LL", out var llVersionProp) &&
                        llVersionProp.TryGetProperty("value", out var versionValueProp) &&
                        versionValueProp.GetString() is { } serverVersion)
                    {
                        lastModified = serverVersion;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[StructureLoad] Failed to retrieve version for metadata: {Error}", ex.Message);
                }

                // Step 6: Write to cache with atomic writes
                try
                {
                    var tmpJsonPath = $"{cacheJsonPath}.tmp";
                    var tmpMetaPath = $"{cacheMetaPath}.tmp";

                    // Write structure file
                    await File.WriteAllTextAsync(tmpJsonPath, jsonContent, cancellationToken).ConfigureAwait(false);

                    // Write metadata
                    var metadata = new StructureCacheMetadata(
                        lastModified,
                        host,
                        serialNumber,
                        DateTime.UtcNow,
                        new FileInfo(tmpJsonPath).Length
                    );
                    var metaJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(tmpMetaPath, metaJson, cancellationToken).ConfigureAwait(false);

                    // Atomic rename
                    File.Move(tmpJsonPath, cacheJsonPath, overwrite: true);
                    File.Move(tmpMetaPath, cacheMetaPath, overwrite: true);

                    _logger.LogInformation("[StructureLoad] Cache updated successfully to {CacheJsonPath}", cacheJsonPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[StructureLoad] Failed to write cache files: {Error}. Continuing with in-memory structure.", ex.Message);
                }
            }
            else if (useCacheOnly)
            {
                if (!File.Exists(cacheJsonPath))
                {
                    throw new InvalidOperationException($"Cache file not found at {cacheJsonPath} and cache-only mode is enabled");
                }
                _logger.LogInformation("[StructureLoad] Loading from cache only: {CacheJsonPath}", cacheJsonPath);
                jsonContent = await File.ReadAllTextAsync(cacheJsonPath, cancellationToken).ConfigureAwait(false);
            }
            else if (string.IsNullOrEmpty(jsonContent))
            {
                throw new InvalidOperationException("No cache file available and cannot fetch from server");
            }

            // Step 7: Parse structure with tolerant converter
            _uuidMap.Clear();
            _roomMap.Clear();
            _categoryMap.Clear();
            _operatingModes.Clear();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // Set the shared logger for all converters
            TolerantDictionaryConverterLogger.Current = _logger;
            
            try
            {
                var structure = JsonSerializer.Deserialize<StructureFileDto>(jsonContent, options);
                if (structure == null)
                    return;

                // Step 8: Build rooms and categories
                if (structure.Rooms is { } rooms)
                {
                    foreach (var kvp in rooms)
                    {
                        try
                        {
                            var dto = kvp.Value;
                            _roomMap[kvp.Key] = new LoxoneRoom(kvp.Key, dto.Name, dto.Image, dto.DefaultRating);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[StructureLoad] Skipping room {RoomId}: {Error}", kvp.Key, ex.Message);
                        }
                    }
                }

                if (structure.Categories is { } cats)
                {
                    foreach (var kvp in cats)
                    {
                        try
                        {
                            var dto = kvp.Value;
                            _categoryMap[kvp.Key] = new LoxoneCategory(kvp.Key, dto.Name, dto.Type, dto.Color);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[StructureLoad] Skipping category {CategoryId}: {Error}", kvp.Key, ex.Message);
                        }
                    }
                }

                // Step 9: Build operating modes
                if (structure.OperatingModes is { } modes)
                {
                    foreach (var kvp in modes)
                    {
                        if (int.TryParse(kvp.Key, out var id))
                        {
                            try
                            {
                                _operatingModes[id] = kvp.Value;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("[StructureLoad] Skipping operating mode {ModeId}: {Error}", kvp.Key, ex.Message);
                            }
                        }
                    }
                }

                // Step 10: Build controls
                if (structure.Controls is { } ctrls)
                {
                    foreach (var kvp in ctrls)
                    {
                        try
                        {
                            AddControl(kvp.Key, kvp.Value, null, null, options);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[StructureLoad] Skipping control {ControlUuid}: {Error}", kvp.Key, ex.Message);
                        }
                    }
                }
            }
            finally
            {
                // Clear the shared logger
                TolerantDictionaryConverterLogger.Current = null;
            }
        }
        finally
        {
            // Step 11: Release file lock
            if (lockStream != null)
            {
                try
                {
                    FileLockHelper.ReleaseLock(lockStream, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[StructureLoad] Failed to release cache lock: {Error}", ex.Message);
                }
            }
        }
    }

    private void AddControl(string uuid, ControlDto dto, string? parentRoomId, string? parentCatId, JsonSerializerOptions options, LoxoneControl? host = null)
    {
        try
        {
            var roomId = dto.Room ?? parentRoomId;
            var catId = dto.Category ?? parentCatId;

            ControlType ctrlType = dto.Type switch
            {
                "Switch" => ControlType.Switch,
                "LightController" => ControlType.LightController,
                "LightControllerV2" => ControlType.LightControllerV2,
                "Dimmer" => ControlType.Dimmer,
                "ColorPickerV2" => ControlType.ColorPickerV2,
                _ => ControlType.Unknown
            };

            var name = dto.Name ?? uuid;
            var roomName = roomId != null && _roomMap.TryGetValue(roomId, out var roomObj) ? roomObj.Name : null;
            var categoryName = catId != null && _categoryMap.TryGetValue(catId, out var catObj) ? catObj.Name : null;

            var control = _lightMode
                ? new LoxoneControl()
                : LoxoneControlFactory.Create(ctrlType);

            control.Uuid = uuid;
            control.Name = name;
            control.Type = ctrlType;
            control.RoomId = roomId;
            control.CategoryId = catId;
            control.RoomName = roomName;
            control.CategoryName = categoryName;
            control.DefaultRating = dto.DefaultRating;
            control.IsSecured = dto.IsSecured;
            control.SecuredDetails = dto.SecuredDetails;
            control.UuidAction = dto.UuidAction;
            control.RawDetails = dto.Details;
            control.Statistic = dto.Statistic;
            control.Restrictions = dto.Restrictions;
            control.HasControlNotes = dto.HasControlNotes;
            control.Preset = dto.Preset is { } p ? new LoxonePreset(p.Uuid, p.Name) : null;
            control.Links = dto.Links;
            control.States = dto.States;

            if (dto.States is not null)
            {
                foreach (var kvp in dto.States)
                {
                    _uuidMap[kvp.Value] = control;
                }
            }

            if (host == null)
            {
                _uuidMap[uuid] = control;

                if (control.UuidAction is { } actionUuid && actionUuid != uuid)
                {
                    _uuidMap[actionUuid] = control;
                }
            }
            else
            {
                host.SubControls[uuid] = control;

                if (control.UuidAction is { } actionUuid)
                {
                    _uuidMap[actionUuid] = control;
                }
            }

            if (dto.SubControls is { } subs)
            {
                foreach (var sub in subs)
                {
                    try
                    {
                        AddControl(sub.Key, sub.Value, roomId, catId, options, control);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[StructureLoad] Skipping subcontrol {SubUuid}: {Error}", sub.Key, ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add control {uuid}", ex);
        }
    }

    public bool TryGetControl(string uuid, out LoxoneControl? control) =>
        _uuidMap.TryGetValue(uuid, out control);

    public bool TryGetControlByName(string name, out LoxoneControl? control)
    {
        control = _uuidMap.Values.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        return control is not null;
    }

    public IEnumerable<LoxoneControl> GetControlsByRoom(string roomName)
    {
        return _uuidMap.Values
            .Where(c => string.Equals(c.RoomName, roomName, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<LoxoneControl> GetControlsByCategory(string categoryName)
    {
        return _uuidMap.Values
            .Where(c => string.Equals(c.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleWebSocketMessage(object? sender, string json)
    {
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Only handle {"uuid":"…","value":"…"} state-update messages emitted by BinaryProtocolParser.
            // Ignore command responses ({"LL":…}) and any other shapes.
            if (!root.TryGetProperty("uuid", out var uuidProp) || !root.TryGetProperty("value", out var valueProp))
                return;

            var uuid = uuidProp.GetString();
            if (uuid is null)
                return;

            var value = valueProp.ValueKind == JsonValueKind.String
                ? valueProp.GetString() ?? string.Empty
                : valueProp.ToString();

            if (_uuidMap.TryGetValue(uuid, out var ctrl) && ctrl.States is not null)
            {
                foreach (var kvp in ctrl.States)
                {
                    if (kvp.Value == uuid)
                    {
                        ctrl.UpdateStateValue(kvp.Key, value);
                        break;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON – silently ignore (e.g. stray binary data)
        }
        finally
        {
            doc?.Dispose();
        }
    }
}

