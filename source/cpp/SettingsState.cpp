// SPDX-FileCopyrightText: Copyright (c) 2023-2026 NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
//

#include "SettingsState.h"

#include "Core.h"
#include "Log.h"
#include "Settings.h"

#include <pxr/pxr.h>

#include <fmt/format.h>

#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <sstream>

namespace
{

using StringMap = std::unordered_map<std::string, std::string>;

static constexpr const char* g_sdkSettingsRelativePath = "config/usd.exporter.revit.core.toml";
static constexpr const char* g_clientSettingsRelativePath = "config/usd.exporter.revit.client.toml";
static constexpr const char* g_userConfigFileName = "user.config.json";
static constexpr const char* g_folderPattern = "{0}/{1}-{2}/{3}-{4}";

#ifdef USD_EXPORTER_REVIT_APP_VERSION
static constexpr const char* g_builtAppVersion = USD_EXPORTER_REVIT_APP_VERSION;
#else
static constexpr const char* g_builtAppVersion = "";
#endif

#ifdef USD_EXPORTER_REVIT_VERSION
static constexpr const char* g_builtPluginVersion = USD_EXPORTER_REVIT_VERSION;
#else
static constexpr const char* g_builtPluginVersion = "";
#endif

static usd::exporter::revit::core::SettingsState g_settingsState;

std::string trim(std::string value)
{
    const auto isSpace = [](unsigned char ch)
    {
        return std::isspace(ch) != 0;
    };
    while (!value.empty() && isSpace(static_cast<unsigned char>(value.front())))
    {
        value.erase(value.begin());
    }
    while (!value.empty() && isSpace(static_cast<unsigned char>(value.back())))
    {
        value.pop_back();
    }
    return value;
}

std::string stripComment(std::string line)
{
    bool inQuotes = false;
    for (size_t index = 0; index < line.size(); ++index)
    {
        if (line[index] == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }
        if (!inQuotes && line[index] == '#')
        {
            line.resize(index);
            break;
        }
    }
    return trim(line);
}

std::string unquote(std::string value)
{
    value = trim(value);
    if (value.size() >= 2 && value.front() == '"' && value.back() == '"')
    {
        return value.substr(1, value.size() - 2);
    }
    return value;
}

std::string parseTableHeader(const std::string& line)
{
    std::string header = trim(line);
    if (header.size() < 2 || header.front() != '[' || header.back() != ']')
    {
        return {};
    }
    header = header.substr(1, header.size() - 2);

    std::string result;
    for (size_t index = 0; index < header.size();)
    {
        if (header[index] == '"')
        {
            const size_t endQuote = header.find('"', index + 1);
            if (endQuote == std::string::npos)
            {
                return {};
            }
            if (!result.empty())
            {
                result.push_back('.');
            }
            result.append(header.substr(index + 1, endQuote - index - 1));
            index = endQuote + 1;
            continue;
        }

        if (header[index] == '.')
        {
            ++index;
            continue;
        }

        const size_t nextDot = header.find('.', index);
        const size_t nextQuote = header.find('"', index);
        size_t end = header.size();
        if (nextDot != std::string::npos && (nextQuote == std::string::npos || nextDot < nextQuote))
        {
            end = nextDot;
        }
        else if (nextQuote != std::string::npos)
        {
            end = nextQuote;
        }

        if (!result.empty())
        {
            result.push_back('.');
        }
        result.append(trim(header.substr(index, end - index)));
        index = end;
    }

    return result;
}

std::string joinKey(const std::string& prefix, const std::string& key)
{
    if (prefix.empty())
    {
        return key;
    }
    if (key.empty())
    {
        return prefix;
    }
    return prefix + "." + key;
}

bool parseTomlFile(const std::filesystem::path& path, StringMap& values)
{
    std::ifstream input(path);
    if (!input.is_open())
    {
        return false;
    }

    std::string currentPrefix;
    std::string line;
    while (std::getline(input, line))
    {
        line = stripComment(line);
        if (line.empty())
        {
            continue;
        }

        if (line.front() == '[')
        {
            currentPrefix = parseTableHeader(line);
            continue;
        }

        const size_t equals = line.find('=');
        if (equals == std::string::npos)
        {
            continue;
        }

        const std::string key = trim(line.substr(0, equals));
        const std::string value = unquote(trim(line.substr(equals + 1)));
        values[joinKey(currentPrefix, key)] = value;
    }

    return true;
}

std::string getEnvironmentVariable(const char* name)
{
    if (name == nullptr || name[0] == '\0')
    {
        return {};
    }

#if defined(_WIN32)
    char* buffer = nullptr;
    size_t bufferLength = 0;
    if (_dupenv_s(&buffer, &bufferLength, name) != 0 || buffer == nullptr)
    {
        return {};
    }
    std::string value(buffer);
    std::free(buffer);
    return value;
#else
    const char* value = std::getenv(name);
    return value != nullptr ? std::string(value) : std::string();
#endif
}

std::string getUserProfilePath()
{
    std::string userProfile = getEnvironmentVariable("USERPROFILE");
    if (userProfile.empty())
    {
        userProfile = getEnvironmentVariable("HOME");
    }
    std::replace(userProfile.begin(), userProfile.end(), '\\', '/');
    return userProfile;
}

std::string getOmniverseBasePath(const char* leaf)
{
    const std::string userProfile = getUserProfilePath();
    if (userProfile.empty())
    {
        return {};
    }
    return userProfile + "/.usd_exporter_revit/" + leaf;
}

bool getTimestampString(std::string& outTimeString)
{
    const std::time_t currentTime = std::time(nullptr);
    std::tm localTime{};
#if defined(_WIN32)
    if (localtime_s(&localTime, &currentTime) != 0)
    {
        return false;
    }
#else
    if (localtime_r(&currentTime, &localTime) == nullptr)
    {
        return false;
    }
#endif

    char timeBuffer[32]{};
    if (std::strftime(timeBuffer, sizeof(timeBuffer), "%Y%m%d_%H%M%S", &localTime) == 0)
    {
        return false;
    }
    outTimeString = timeBuffer;
    return true;
}

bool isDebugBuild()
{
#ifdef _DEBUG
    return true;
#else
    return false;
#endif
}

std::string resolveSubstitutionExpression(const std::string& expression, const StringMap& tokens)
{
    if (expression.rfind("env:", 0) == 0)
    {
        return getEnvironmentVariable(expression.substr(4).c_str());
    }

    const auto found = tokens.find(expression);
    if (found != tokens.end())
    {
        return found->second;
    }

    return "${" + expression + "}";
}

std::string resolveSubstitutions(const std::string& input, const StringMap& tokens)
{
    std::string result;
    result.reserve(input.size());

    for (size_t index = 0; index < input.size();)
    {
        if (input[index] == '$' && index + 1 < input.size() && input[index + 1] == '{')
        {
            const size_t end = input.find('}', index + 2);
            if (end != std::string::npos)
            {
                const std::string expression = input.substr(index + 2, end - index - 2);
                result.append(resolveSubstitutionExpression(expression, tokens));
                index = end + 1;
                continue;
            }
        }

        result.push_back(input[index]);
        ++index;
    }

    return result;
}

void resolveAllValues(StringMap& values, const StringMap& tokens)
{
    for (auto& entry : values)
    {
        entry.second = resolveSubstitutions(entry.second, tokens);
    }
}

void migrateLegacyKeys(StringMap& values, const char* legacyPrefix, const char* newPrefix)
{
    const std::string legacy = legacyPrefix;
    const std::string replacement = newPrefix;
    const size_t legacyLength = legacy.size();

    StringMap migrated;
    for (const auto& entry : values)
    {
        if (entry.first.rfind(legacy, 0) == 0)
        {
            migrated[replacement + entry.first.substr(legacyLength)] = entry.second;
        }
    }

    if (migrated.empty())
    {
        return;
    }

    for (const auto& entry : migrated)
    {
        if (values.find(entry.first) == values.end())
        {
            values[entry.first] = entry.second;
        }
    }
}

void applyPersistentPreferences(StringMap& values)
{
    const std::string persistentPrefix = "persistent.";
    StringMap overrides;
    for (const auto& entry : values)
    {
        if (entry.first.rfind(persistentPrefix, 0) != 0)
        {
            continue;
        }

        const std::string targetKey = entry.first.substr(persistentPrefix.size());
        overrides[targetKey] = entry.second;
    }

    for (const auto& entry : overrides)
    {
        values[entry.first] = entry.second;
    }
}

void extractOptionGroup(const StringMap& values, const char* prefix, std::unordered_map<std::string, std::string>& options)
{
    const std::string optionPrefix = prefix;
    const size_t prefixLength = optionPrefix.size();
    for (const auto& entry : values)
    {
        if (entry.first.rfind(optionPrefix, 0) != 0)
        {
            continue;
        }
        options[entry.first.substr(prefixLength)] = entry.second;
    }
}

std::string valueAtPath(const StringMap& values, const char* path)
{
    const auto found = values.find(path);
    return found != values.end() ? found->second : std::string();
}

bool parseBool(const std::string& value)
{
    const std::string normalized = trim(value);
    return normalized == "true" || normalized == "1" || normalized == "True" || normalized == "TRUE";
}

void skipJsonWhitespace(const std::string& json, size_t& index)
{
    while (index < json.size() && std::isspace(static_cast<unsigned char>(json[index])) != 0)
    {
        ++index;
    }
}

bool parseJsonString(const std::string& json, size_t& index, std::string& outValue)
{
    skipJsonWhitespace(json, index);
    if (index >= json.size() || json[index] != '"')
    {
        return false;
    }
    ++index;

    std::string value;
    while (index < json.size())
    {
        const char ch = json[index++];
        if (ch == '"')
        {
            outValue = value;
            return true;
        }
        if (ch == '\\' && index < json.size())
        {
            const char escaped = json[index++];
            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    value.push_back(escaped);
                    break;
                case 'b':
                    value.push_back('\b');
                    break;
                case 'f':
                    value.push_back('\f');
                    break;
                case 'n':
                    value.push_back('\n');
                    break;
                case 'r':
                    value.push_back('\r');
                    break;
                case 't':
                    value.push_back('\t');
                    break;
                default:
                    value.push_back(escaped);
                    break;
            }
            continue;
        }
        value.push_back(ch);
    }
    return false;
}

bool parseJsonValue(const std::string& json, size_t& index, const std::string& prefix, StringMap& values);

bool parseJsonObject(const std::string& json, size_t& index, const std::string& prefix, StringMap& values)
{
    skipJsonWhitespace(json, index);
    if (index >= json.size() || json[index] != '{')
    {
        return false;
    }
    ++index;

    while (index < json.size())
    {
        skipJsonWhitespace(json, index);
        if (index < json.size() && json[index] == '}')
        {
            ++index;
            return true;
        }

        std::string key;
        if (!parseJsonString(json, index, key))
        {
            return false;
        }

        skipJsonWhitespace(json, index);
        if (index >= json.size() || json[index] != ':')
        {
            return false;
        }
        ++index;

        if (!parseJsonValue(json, index, joinKey(prefix, key), values))
        {
            return false;
        }

        skipJsonWhitespace(json, index);
        if (index < json.size() && json[index] == ',')
        {
            ++index;
            continue;
        }
        if (index < json.size() && json[index] == '}')
        {
            ++index;
            return true;
        }
        return false;
    }
    return false;
}

bool parseJsonValue(const std::string& json, size_t& index, const std::string& prefix, StringMap& values)
{
    skipJsonWhitespace(json, index);
    if (index >= json.size())
    {
        return false;
    }

    const char ch = json[index];
    if (ch == '{')
    {
        return parseJsonObject(json, index, prefix, values);
    }
    if (ch == '"')
    {
        std::string value;
        if (!parseJsonString(json, index, value))
        {
            return false;
        }
        if (!prefix.empty())
        {
            values[prefix] = value;
        }
        return true;
    }
    if (ch == 't' || ch == 'f')
    {
        const bool isTrue = json.compare(index, 4, "true") == 0;
        const bool isFalse = json.compare(index, 5, "false") == 0;
        if (!isTrue && !isFalse)
        {
            return false;
        }
        if (!prefix.empty())
        {
            values[prefix] = isTrue ? "true" : "false";
        }
        index += isTrue ? 4 : 5;
        return true;
    }
    if (ch == '-' || std::isdigit(static_cast<unsigned char>(ch)) != 0)
    {
        const size_t start = index;
        if (json[index] == '-')
        {
            ++index;
        }
        while (index < json.size() && (std::isdigit(static_cast<unsigned char>(json[index])) != 0 || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
        {
            ++index;
        }
        if (!prefix.empty())
        {
            values[prefix] = json.substr(start, index - start);
        }
        return true;
    }
    if (ch == '[' || ch == 'n')
    {
        while (index < json.size() && json[index] != ',' && json[index] != '}' && json[index] != ']')
        {
            ++index;
        }
        return true;
    }

    return false;
}

bool parseJsonPreferencesFile(const std::filesystem::path& path, StringMap& values)
{
    std::ifstream input(path);
    if (!input.is_open())
    {
        return false;
    }

    std::ostringstream buffer;
    buffer << input.rdbuf();
    const std::string json = buffer.str();
    size_t index = 0;
    return parseJsonObject(json, index, "", values);
}

std::string buildScopedFolder(const std::string& basePath, const std::string& appName, const std::string& appVersion, const std::string& clientName, const std::string& clientVersion)
{
    std::string folder = fmt::format(g_folderPattern, basePath, appName, appVersion, clientName, clientVersion);
    std::replace(folder.begin(), folder.end(), '\\', '/');
    return folder;
}

} // namespace

namespace usd::exporter::revit::core
{

std::string SettingsState::exportOption(const std::string& key) const
{
    const auto found = exportOptions.find(key);
    return found != exportOptions.end() ? found->second : std::string();
}

std::string SettingsState::importOption(const std::string& key) const
{
    const auto found = importOptions.find(key);
    return found != importOptions.end() ? found->second : std::string();
}

bool loadSettingsState(SettingsState& state, bool preserveStartTimestamp)
{
    StringMap values;

    const std::filesystem::path sdkSettingsFile = std::filesystem::path(usd_exporter_revit_install_path()) / g_sdkSettingsRelativePath;
    parseTomlFile(sdkSettingsFile, values);

    const std::filesystem::path clientSettingsFile = std::filesystem::path(usd_exporter_revit_install_path()) / g_clientSettingsRelativePath;
    if (std::filesystem::exists(clientSettingsFile))
    {
        parseTomlFile(clientSettingsFile, values);
    }

    if (!preserveStartTimestamp || state.startTimestamp.empty())
    {
        if (!getTimestampString(state.startTimestamp))
        {
            USD_EXPORTER_REVIT_LOG_ERROR(kUsdExporterRevitChannel, "Time conversion failure! Falling back to 00000000_000000.");
            state.startTimestamp = "00000000_000000";
        }
    }

    state.usdVersion = fmt::format("{0}.{1}.{2}", PXR_MAJOR_VERSION, PXR_MINOR_VERSION, PXR_PATCH_VERSION);
    state.buildConfig = isDebugBuild() ? "debug" : "release";

    StringMap preValidationTokens;
    preValidationTokens[kUsdVersionToken] = state.usdVersion;
    preValidationTokens[kStartTimeToken] = state.startTimestamp;
    preValidationTokens[kBuildConfigToken] = state.buildConfig;

    for (const char* key : { "app.name", "usd.exporter.revit.core.client.name" })
    {
        auto found = values.find(key);
        if (found != values.end())
        {
            found->second = resolveSubstitutions(found->second, preValidationTokens);
        }
    }

    state.appName = valueAtPath(values, "app.name");
    state.appVersion = g_builtAppVersion;
    state.clientName = valueAtPath(values, "usd.exporter.revit.core.client.name");
    state.clientVersion = g_builtPluginVersion;
    state.waitForDebugger = parseBool(valueAtPath(values, "app.waitForDebugger"));

    if (state.clientName.empty() || state.clientVersion.empty() || state.appName.empty() || state.appVersion.empty())
    {
        static constexpr const char* badSettingsMessage = R"(All of The following settings must be specified:

        %s = "%s"
        %s = "%s"
        %s = "%s"
        %s = "%s"

    Check "%s" to verify these settings.
            )";

        USD_EXPORTER_REVIT_LOG_FATAL(
            badSettingsMessage,
            kAppNameSetting,
            state.appName.c_str(),
            kAppVersionSetting,
            state.appVersion.c_str(),
            kClientNameSetting,
            state.clientName.c_str(),
            kClientVersionSetting,
            state.clientVersion.c_str(),
            clientSettingsFile.string().c_str()
        );
        return false;
    }

    state.dataFolder = buildScopedFolder(getOmniverseBasePath("data"), state.appName, state.appVersion, state.clientName, state.clientVersion);
    state.logsFolder = buildScopedFolder(getOmniverseBasePath("logs"), state.appName, state.appVersion, state.clientName, state.clientVersion);
    state.cacheFolder = buildScopedFolder(getOmniverseBasePath("cache"), state.appName, state.appVersion, state.clientName, state.clientVersion);

    const std::filesystem::path preferencesConfigPath = std::filesystem::path(state.dataFolder) / g_userConfigFileName;
    if (std::filesystem::exists(preferencesConfigPath))
    {
        parseJsonPreferencesFile(preferencesConfigPath, values);
    }

    migrateLegacyKeys(values, "revit.connect.core", "usd.exporter.revit.core");
    migrateLegacyKeys(values, "persistent.revit.connect.core", "persistent.usd.exporter.revit.core");
    migrateLegacyKeys(values, "revit.usd.export.core", "usd.exporter.revit.core");
    migrateLegacyKeys(values, "persistent.revit.usd.export.core", "persistent.usd.exporter.revit.core");
    applyPersistentPreferences(values);

    StringMap tokens;
    tokens[kAppNameToken] = state.appName;
    tokens[kAppVersionToken] = state.appVersion;
    tokens[kClientNameToken] = state.clientName;
    tokens[kClientVersionToken] = state.clientVersion;
    tokens[kUsdVersionToken] = state.usdVersion;
    tokens[kStartTimeToken] = state.startTimestamp;
    tokens[kBuildConfigToken] = state.buildConfig;
    tokens[kDataFolderToken] = state.dataFolder;
    tokens[kLogFolderToken] = state.logsFolder;
    tokens[kCacheFolderToken] = state.cacheFolder;

    resolveAllValues(values, tokens);

    state.appName = valueAtPath(values, "app.name");
    state.clientName = valueAtPath(values, "usd.exporter.revit.core.client.name");
    state.logFile = resolveSubstitutions(valueAtPath(values, "log.file"), tokens);
    if (state.logLevel.empty())
    {
        state.logLevel = resolveSubstitutions(valueAtPath(values, "log.level"), tokens);
    }
    else
    {
        state.logLevel = resolveSubstitutions(state.logLevel, tokens);
    }

    extractOptionGroup(values, "usd.exporter.revit.core.exportOptions.", state.exportOptions);
    extractOptionGroup(values, "usd.exporter.revit.core.importOptions.", state.importOptions);

    for (auto& entry : state.exportOptions)
    {
        entry.second = resolveSubstitutions(entry.second, tokens);
    }
    for (auto& entry : state.importOptions)
    {
        entry.second = resolveSubstitutions(entry.second, tokens);
    }

    return true;
}

SettingsState& mutableSettingsState()
{
    return g_settingsState;
}

const SettingsState& settingsState()
{
    return g_settingsState;
}

} // namespace usd::exporter::revit::core
