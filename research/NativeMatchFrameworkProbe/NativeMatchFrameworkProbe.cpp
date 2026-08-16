using BOOL = int;
using DWORD = unsigned long;
using HANDLE = void *;
using HMODULE = void *;
using UINT = unsigned int;

extern "C" HMODULE __stdcall LoadLibraryExW(const wchar_t *, HANDLE, DWORD);
extern "C" void * __stdcall GetProcAddress(HMODULE, const char *);
extern "C" DWORD __stdcall GetLastError();
extern "C" HANDLE __stdcall GetStdHandle(DWORD);
extern "C" BOOL __stdcall WriteFile(HANDLE, const void *, DWORD, DWORD *, void *);
extern "C" int __stdcall lstrlenA(const char *);
extern "C" BOOL __stdcall SetCurrentDirectoryW(const wchar_t *);
extern "C" void __stdcall Sleep(DWORD);
extern "C" void __stdcall ExitProcess(UINT);

extern "C" void *__cdecl memcpy(void *destination, const void *source, unsigned int size)
{
    auto destinationBytes = reinterpret_cast<unsigned char *>(destination);
    const auto sourceBytes = reinterpret_cast<const unsigned char *>(source);
    for (unsigned int index = 0; index < size; ++index)
        destinationBytes[index] = sourceBytes[index];
    return destination;
}

static constexpr DWORD STD_OUTPUT_HANDLE = static_cast<DWORD>(-11);
static constexpr DWORD LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

using CreateInterfaceFn = void *(__cdecl *)(const char *, int *);
using SteamInitFn = bool (__cdecl *)();
using ConnectFn = bool (__thiscall *)(void *, CreateInterfaceFn);
using InitFn = int (__thiscall *)(void *);
using GetNoArgFn = void *(__thiscall *)(void *);
using CreateSessionFn = void (__thiscall *)(void *, void *);
using SteamRunCallbacksFn = void (__cdecl *)();
using SteamApiCall = unsigned long long;
using SteamMatchmakingInterfaceFn = void *(__cdecl *)();
using SteamUtilsInterfaceFn = void *(__cdecl *)();
using SteamRequestLobbyListFn = SteamApiCall (__cdecl *)(void *);
using SteamIsApiCallCompletedFn = bool (__cdecl *)(void *, SteamApiCall, bool *);
using SteamGetApiCallResultFn = bool (__cdecl *)(void *, SteamApiCall, void *, int, int, bool *);
using SteamGetLobbyByIndexFn = SteamApiCall (__cdecl *)(void *, int);
using SteamGetLobbyDataFn = const char *(__cdecl *)(void *, SteamApiCall, const char *);

using KeyValuesSystemFn = void *(__cdecl *)();
using KeyValuesRegisterSizeFn = void (__thiscall *)(void *, int);
using KeyValuesAllocFn = void *(__thiscall *)(void *, int);
using KeyValuesGetSymbolFn = int (__thiscall *)(void *, const char *, bool);
using KeyValuesGetStringFn = const char *(__thiscall *)(void *, int);
using MemAllocFn = void *(__thiscall *)(void *, unsigned int, const char *, int);

static constexpr int KEYVALUES_SIZE = 44;
static constexpr char KEYVALUES_TYPE_STRING = 1;
static constexpr char KEYVALUES_TYPE_INT = 2;
static constexpr char KEYVALUES_TYPE_UINT64 = 7;
static constexpr int STEAM_CALLBACK_LOBBY_MATCH_LIST = 510;

struct RawKeyValues
{
    int keyName;
    char *stringValue;
    wchar_t *wideStringValue;
    union
    {
        int integerValue;
        float floatValue;
        void *pointerValue;
        unsigned char color[4];
    } value;
    char dataType;
    char hasEscapeSequences;
    char unused[2];
    void *keyValuesSystem;
    char ownsCustomKeyValuesSystem;
    char alignmentPadding[3];
    RawKeyValues *peer;
    RawKeyValues *sub;
    RawKeyValues *chain;
    void *expressionGetSymbolProc;
};

static_assert(sizeof(RawKeyValues) == KEYVALUES_SIZE, "Unexpected L4D2 KeyValues layout");

static HANDLE g_stdout;
static HMODULE g_tier0;
static HMODULE g_vstdlib;
static HMODULE g_filesystem;
static HMODULE g_vgui2;
static HMODULE g_engine;
static HMODULE g_client;
static HMODULE g_server;
static HMODULE g_matchmaking;
static HMODULE g_steam;
static void *g_key_values_system;
static CreateInterfaceFn g_vstdlib_factory;
static CreateInterfaceFn g_filesystem_factory;
static CreateInterfaceFn g_vgui2_factory;
static CreateInterfaceFn g_engine_factory;
static CreateInterfaceFn g_client_factory;
static CreateInterfaceFn g_server_factory;

static void ZeroMemory(void *memory, unsigned int size)
{
    auto bytes = reinterpret_cast<unsigned char *>(memory);
    for (unsigned int index = 0; index < size; ++index)
        bytes[index] = 0;
}

static unsigned int StringLength(const char *value)
{
    unsigned int length = 0;
    while (value[length] != '\0')
        ++length;
    return length;
}

static bool StringEquals(const char *left, const char *right)
{
    if (!left || !right)
        return false;

    unsigned int index = 0;
    while (left[index] != '\0' && right[index] != '\0')
    {
        if (left[index] != right[index])
            return false;
        ++index;
    }

    return left[index] == right[index];
}

static void CopyString(char *destination, const char *source)
{
    const auto length = StringLength(source);
    for (unsigned int index = 0; index <= length; ++index)
        destination[index] = source[index];
}

static void WriteText(const char *text)
{
    DWORD written = 0;
    const auto length = lstrlenA(text);
    WriteFile(g_stdout, text, static_cast<DWORD>(length), &written, nullptr);
}

static void WriteHex(unsigned long value)
{
    char buffer[11] = "0x00000000";
    const char *digits = "0123456789ABCDEF";
    for (int index = 9; index >= 2; --index)
    {
        buffer[index] = digits[value & 0xF];
        value >>= 4;
    }
    WriteText(buffer);
}

static void WriteDecimal(unsigned long value)
{
    char buffer[11];
    int index = 10;
    buffer[index] = '\0';
    do
    {
        buffer[--index] = static_cast<char>('0' + (value % 10));
        value /= 10;
    } while (value != 0);
    WriteText(buffer + index);
}

static void WriteUInt64(unsigned long long value)
{
    char buffer[19] = "0x0000000000000000";
    const char *digits = "0123456789ABCDEF";
    for (int index = 17; index >= 2; --index)
    {
        buffer[index] = digits[static_cast<unsigned int>(value & 0xF)];
        value >>= 4;
    }
    WriteText(buffer);
}

static void WriteLoadResult(const wchar_t *name, HMODULE module)
{
    WriteText("load ");
    char shortName[64];
    int index = 0;
    for (; name[index] != L'\0' && index < static_cast<int>(sizeof(shortName) - 1); ++index)
        shortName[index] = static_cast<char>(name[index]);
    shortName[index] = '\0';
    WriteText(shortName);
    WriteText(module ? " ok\n" : " failed win32=");
    if (!module)
    {
        WriteDecimal(GetLastError());
        WriteText("\n");
    }
}

static HMODULE LoadGameModule(const wchar_t *path)
{
    return LoadLibraryExW(path, nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
}

static void *GetMemAlloc(HMODULE module)
{
    auto exportedAddress = GetProcAddress(module, "g_pMemAlloc");
    if (!exportedAddress)
        return nullptr;
    return *reinterpret_cast<void **>(exportedAddress);
}

static RawKeyValues *AllocateKeyValues(void *keyValuesSystem, const char *name)
{
    auto vtable = *reinterpret_cast<void ***>(keyValuesSystem);
    auto registerSize = reinterpret_cast<KeyValuesRegisterSizeFn>(vtable[1]);
    auto allocate = reinterpret_cast<KeyValuesAllocFn>(vtable[2]);
    auto getSymbol = reinterpret_cast<KeyValuesGetSymbolFn>(vtable[4]);
    registerSize(keyValuesSystem, KEYVALUES_SIZE);

    auto keyValues = reinterpret_cast<RawKeyValues *>(allocate(keyValuesSystem, KEYVALUES_SIZE));
    if (!keyValues)
        return nullptr;

    ZeroMemory(keyValues, KEYVALUES_SIZE);
    keyValues->keyName = getSymbol(keyValuesSystem, name, true);
    keyValues->keyValuesSystem = keyValuesSystem;
    return keyValues;
}

static bool SetStringValue(void *memAlloc, RawKeyValues *keyValues, const char *value)
{
    auto vtable = *reinterpret_cast<void ***>(memAlloc);
    auto allocate = reinterpret_cast<MemAllocFn>(vtable[0]);
    const auto length = StringLength(value);
    auto copy = reinterpret_cast<char *>(allocate(memAlloc, length + 1, "NativeMatchFrameworkProbe", 0));
    if (!copy)
        return false;

    CopyString(copy, value);
    keyValues->stringValue = copy;
    keyValues->dataType = KEYVALUES_TYPE_STRING;
    return true;
}

static void SetIntegerValue(RawKeyValues *keyValues, int value)
{
    keyValues->value.integerValue = value;
    keyValues->dataType = KEYVALUES_TYPE_INT;
}

static void AddSubKey(RawKeyValues *parent, RawKeyValues *child)
{
    child->peer = parent->sub;
    parent->sub = child;
}

static void WriteIndent(int depth)
{
    for (int index = 0; index < depth; ++index)
        WriteText("  ");
}

static void DumpKeyValues(const RawKeyValues *keyValues, int depth)
{
    if (!keyValues)
        return;

    auto systemVtable = *reinterpret_cast<void ***>(g_key_values_system);
    auto getString = reinterpret_cast<KeyValuesGetStringFn>(systemVtable[5]);
    const auto name = getString(g_key_values_system, keyValues->keyName);
    WriteIndent(depth);
    WriteText("KV ");
    WriteText(name ? name : "<unknown>");
    WriteText(" type=");
    WriteDecimal(static_cast<unsigned long>(static_cast<unsigned char>(keyValues->dataType)));
    WriteText(" value=");
    if (keyValues->dataType == KEYVALUES_TYPE_STRING)
        WriteText(keyValues->stringValue ? keyValues->stringValue : "<null>");
    else if (keyValues->dataType == KEYVALUES_TYPE_INT)
        WriteDecimal(static_cast<unsigned long>(keyValues->value.integerValue));
    else if (keyValues->dataType == KEYVALUES_TYPE_UINT64)
    {
        const auto value = reinterpret_cast<const unsigned long long *>(keyValues->value.pointerValue);
        if (value)
            WriteUInt64(*value);
        else
            WriteText("<null>");
    }
    else
        WriteText("<none>");
    WriteText("\n");

    for (auto child = keyValues->sub; child; child = child->peer)
        DumpKeyValues(child, depth + 1);
}

static void DumpMatchSession(void *session)
{
    auto vtable = *reinterpret_cast<void ***>(session);
    auto getSystemData = reinterpret_cast<GetNoArgFn>(vtable[0]);
    auto getSettings = reinterpret_cast<GetNoArgFn>(vtable[1]);
    auto systemData = getSystemData(session);
    auto settings = getSettings(session);

    WriteText("Session system data pointer=");
    WriteHex(reinterpret_cast<unsigned long>(systemData));
    WriteText("\n");
    if (systemData)
        DumpKeyValues(reinterpret_cast<const RawKeyValues *>(systemData), 0);

    WriteText("Session settings pointer=");
    WriteHex(reinterpret_cast<unsigned long>(settings));
    WriteText("\n");
    if (settings)
        DumpKeyValues(reinterpret_cast<const RawKeyValues *>(settings), 0);
}

static RawKeyValues *BuildSessionSettings()
{
    auto keyValuesSystemExport = GetProcAddress(g_vstdlib, "KeyValuesSystem");
    auto memAllocExport = GetProcAddress(g_tier0, "g_pMemAlloc");
    WriteText("KeyValuesSystem export=");
    WriteHex(reinterpret_cast<unsigned long>(keyValuesSystemExport));
    WriteText(" g_pMemAlloc export=");
    WriteHex(reinterpret_cast<unsigned long>(memAllocExport));
    WriteText("\n");

    auto keyValuesSystemFactory = reinterpret_cast<KeyValuesSystemFn>(keyValuesSystemExport);
    if (!keyValuesSystemFactory)
    {
        WriteText("KeyValuesSystem export missing\n");
        return nullptr;
    }

    auto keyValuesSystem = keyValuesSystemFactory();
    auto memAlloc = GetMemAlloc(g_tier0);
    WriteText("KeyValuesSystem pointer=");
    WriteHex(reinterpret_cast<unsigned long>(keyValuesSystem));
    WriteText(" g_pMemAlloc pointer=");
    WriteHex(reinterpret_cast<unsigned long>(memAlloc));
    WriteText("\n");
    if (!keyValuesSystem || !memAlloc)
    {
        WriteText("KeyValues services unavailable\n");
        return nullptr;
    }

    g_key_values_system = keyValuesSystem;

    auto settings = AllocateKeyValues(keyValuesSystem, "settings");
    auto game = AllocateKeyValues(keyValuesSystem, "Game");
    auto system = AllocateKeyValues(keyValuesSystem, "System");
    auto members = AllocateKeyValues(keyValuesSystem, "Members");
    if (!settings || !game || !system || !members)
    {
        WriteText("KeyValues allocation failed\n");
        return nullptr;
    }

    auto mode = AllocateKeyValues(keyValuesSystem, "mode");
    auto map = AllocateKeyValues(keyValuesSystem, "map");
    auto campaign = AllocateKeyValues(keyValuesSystem, "campaign");
    auto chapter = AllocateKeyValues(keyValuesSystem, "chapter");
    auto difficulty = AllocateKeyValues(keyValuesSystem, "difficulty");
    auto state = AllocateKeyValues(keyValuesSystem, "state");
    auto vanilla = AllocateKeyValues(keyValuesSystem, "vanilla");
    auto network = AllocateKeyValues(keyValuesSystem, "network");
    auto access = AllocateKeyValues(keyValuesSystem, "access");
    auto numSlots = AllocateKeyValues(keyValuesSystem, "numSlots");
    if (!mode || !map || !campaign || !chapter || !difficulty || !state || !vanilla ||
        !network || !access || !numSlots)
    {
        WriteText("KeyValues field allocation failed\n");
        return nullptr;
    }

    if (!SetStringValue(memAlloc, mode, "coop") ||
        !SetStringValue(memAlloc, map, "c1m1_hotel") ||
        !SetStringValue(memAlloc, campaign, "c1m1") ||
        !SetStringValue(memAlloc, difficulty, "normal") ||
        !SetStringValue(memAlloc, state, "lobby") ||
        !SetStringValue(memAlloc, network, "LIVE") ||
        !SetStringValue(memAlloc, access, "public"))
    {
        WriteText("KeyValues string allocation failed\n");
        return nullptr;
    }

    SetIntegerValue(chapter, 1);
    SetIntegerValue(vanilla, 1);
    SetIntegerValue(numSlots, 4);

    AddSubKey(game, mode);
    AddSubKey(game, map);
    AddSubKey(game, campaign);
    AddSubKey(game, chapter);
    AddSubKey(game, difficulty);
    AddSubKey(game, state);
    AddSubKey(game, vanilla);
    AddSubKey(system, network);
    AddSubKey(system, access);
    AddSubKey(members, numSlots);
    AddSubKey(settings, game);
    AddSubKey(settings, system);
    AddSubKey(settings, members);
    return settings;
}

static CreateInterfaceFn GetFactory(HMODULE module)
{
    if (!module)
        return nullptr;
    return reinterpret_cast<CreateInterfaceFn>(GetProcAddress(module, "CreateInterface"));
}

static void LogRequestedInterface(const char *name, void *value, int *resultCode, const char *provider)
{
    WriteText("factory ");
    WriteText(name ? name : "<null>");
    WriteText(" provider=");
    WriteText(provider);
    WriteText(" pointer=");
    WriteHex(reinterpret_cast<unsigned long>(value));
    WriteText(" return_code=");
    if (resultCode)
        WriteDecimal(static_cast<unsigned long>(*resultCode));
    else
        WriteText("<null>");
    WriteText("\n");
}

static void *TryFactory(CreateInterfaceFn factory, const char *provider, const char *name, int *resultCode)
{
    if (!factory)
        return nullptr;

    auto value = factory(name, resultCode);
    if (value)
        LogRequestedInterface(name, value, resultCode, provider);
    return value;
}

static void *AggregatedFactory(const char *name, int *resultCode)
{
    if (resultCode)
        *resultCode = 1;

    void *value = TryFactory(g_vstdlib_factory, "vstdlib", name, resultCode);
    if (value)
        return value;

    value = TryFactory(g_filesystem_factory, "filesystem", name, resultCode);
    if (value)
        return value;

    value = TryFactory(g_vgui2_factory, "vgui2", name, resultCode);
    if (value)
        return value;

    value = TryFactory(g_engine_factory, "engine", name, resultCode);
    if (value)
        return value;

    value = TryFactory(g_client_factory, "client", name, resultCode);
    if (value)
        return value;

    value = TryFactory(g_server_factory, "server", name, resultCode);
    if (value)
        return value;

    WriteText("factory ");
    WriteText(name ? name : "<null>");
    WriteText(" provider=<missing> pointer=0x00000000 return_code=");
    if (resultCode)
        WriteDecimal(static_cast<unsigned long>(*resultCode));
    else
        WriteText("<null>");
    WriteText("\n");
    return nullptr;
}

static void *GetMatchFramework()
{
    auto factory = GetFactory(g_matchmaking);
    if (!factory)
    {
        WriteText("matchmaking CreateInterface export missing\n");
        return nullptr;
    }

    int resultCode = -1;
    auto framework = factory("MATCHFRAMEWORK_001", &resultCode);
    WriteText("MATCHFRAMEWORK_001 pointer=");
    WriteHex(reinterpret_cast<unsigned long>(framework));
    WriteText(" return_code=");
    WriteDecimal(static_cast<unsigned long>(resultCode));
    WriteText("\n");
    return framework;
}

static void WriteSteamLobbyData(SteamApiCall lobby, const char *key, const char *value)
{
    WriteText("Steam lobby data id=");
    WriteUInt64(lobby);
    WriteText(" ");
    WriteText(key);
    WriteText("=");
    WriteText(value ? value : "<null>");
    WriteText("\n");
}

static void WriteSteamLobbyQueryUnavailable(bool completed)
{
    WriteText("Steam lobby query completed=");
    WriteText(completed ? "true\n" : "false\n");
    WriteText("Steam lobby match=unavailable\n");
}

static void QuerySteamLobbies()
{
    if (!g_steam)
    {
        WriteSteamLobbyQueryUnavailable(false);
        return;
    }

    auto runCallbacks = reinterpret_cast<SteamRunCallbacksFn>(GetProcAddress(g_steam, "SteamAPI_RunCallbacks"));
    auto getMatchmaking = reinterpret_cast<SteamMatchmakingInterfaceFn>(
        GetProcAddress(g_steam, "SteamAPI_SteamMatchmaking_v009"));
    auto getUtils = reinterpret_cast<SteamUtilsInterfaceFn>(GetProcAddress(g_steam, "SteamAPI_SteamUtils_v010"));
    auto requestLobbyList = reinterpret_cast<SteamRequestLobbyListFn>(
        GetProcAddress(g_steam, "SteamAPI_ISteamMatchmaking_RequestLobbyList"));
    auto isApiCallCompleted = reinterpret_cast<SteamIsApiCallCompletedFn>(
        GetProcAddress(g_steam, "SteamAPI_ISteamUtils_IsAPICallCompleted"));
    auto getApiCallResult = reinterpret_cast<SteamGetApiCallResultFn>(
        GetProcAddress(g_steam, "SteamAPI_ISteamUtils_GetAPICallResult"));
    auto getLobbyByIndex = reinterpret_cast<SteamGetLobbyByIndexFn>(
        GetProcAddress(g_steam, "SteamAPI_ISteamMatchmaking_GetLobbyByIndex"));
    auto getLobbyData = reinterpret_cast<SteamGetLobbyDataFn>(
        GetProcAddress(g_steam, "SteamAPI_ISteamMatchmaking_GetLobbyData"));

    if (!runCallbacks || !getMatchmaking || !getUtils || !requestLobbyList || !isApiCallCompleted ||
        !getApiCallResult || !getLobbyByIndex || !getLobbyData)
    {
        WriteSteamLobbyQueryUnavailable(false);
        return;
    }

    auto matchmaking = getMatchmaking();
    auto utils = getUtils();
    if (!matchmaking || !utils)
    {
        WriteSteamLobbyQueryUnavailable(false);
        return;
    }

    const auto call = requestLobbyList(matchmaking);
    WriteText("Steam lobby query call=");
    WriteUInt64(call);
    WriteText("\n");
    if (!call)
    {
        WriteSteamLobbyQueryUnavailable(false);
        return;
    }

    bool callFailed = false;
    bool completed = false;
    unsigned int lobbyCount = 0;
    for (unsigned int attempt = 0; attempt < 100; ++attempt)
    {
        runCallbacks();
        callFailed = false;
        if (isApiCallCompleted(utils, call, &callFailed))
        {
            completed = true;
            break;
        }
        Sleep(50);
    }

    WriteText("Steam lobby query completed=");
    WriteText(completed ? "true\n" : "false\n");
    if (!completed || callFailed)
    {
        WriteText("Steam lobby match=unavailable\n");
        return;
    }

    callFailed = false;
    const auto resultOk = getApiCallResult(
        utils,
        call,
        &lobbyCount,
        static_cast<int>(sizeof(lobbyCount)),
        STEAM_CALLBACK_LOBBY_MATCH_LIST,
        &callFailed);
    WriteText("Steam lobby query result_ok=");
    WriteText(resultOk ? "true" : "false");
    WriteText(" failed=");
    WriteText(callFailed ? "true\n" : "false\n");
    if (!resultOk || callFailed)
    {
        WriteText("Steam lobby match=unavailable\n");
        return;
    }

    WriteText("Steam lobby count=");
    WriteDecimal(static_cast<unsigned long>(lobbyCount));
    WriteText("\n");

    bool matched = false;
    if (lobbyCount > 0)
    {
        for (unsigned int index = 0; index < lobbyCount; ++index)
        {
            const auto lobby = getLobbyByIndex(matchmaking, static_cast<int>(index));
            if (!lobby)
                continue;

            const auto map = getLobbyData(matchmaking, lobby, "game:map");
            const auto mode = getLobbyData(matchmaking, lobby, "game:mode");
            const auto network = getLobbyData(matchmaking, lobby, "system:network");
            WriteText("Steam lobby id=");
            WriteUInt64(lobby);
            WriteText("\n");
            WriteSteamLobbyData(lobby, "game:map", map);
            WriteSteamLobbyData(lobby, "game:mode", mode);
            WriteSteamLobbyData(lobby, "system:network", network);

            if (StringEquals(map, "c1m1_hotel") && StringEquals(mode, "coop") && StringEquals(network, "LIVE"))
                matched = true;
        }
    }

    WriteText("Steam lobby match=");
    WriteText(matched ? "true\n" : "false\n");
}

static int Run()
{
    g_stdout = GetStdHandle(STD_OUTPUT_HANDLE);
    SetCurrentDirectoryW(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2");

    const auto gameBin = L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\";
    const auto gameBin2 = L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\left4dead2\\bin\\";

    g_tier0 = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\tier0.dll");
    WriteLoadResult(L"tier0.dll", g_tier0);
    g_vstdlib = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\vstdlib.dll");
    WriteLoadResult(L"vstdlib.dll", g_vstdlib);
    g_filesystem = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\filesystem_stdio.dll");
    WriteLoadResult(L"filesystem_stdio.dll", g_filesystem);
    g_vgui2 = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\vgui2.dll");
    WriteLoadResult(L"vgui2.dll", g_vgui2);
    g_engine = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\engine.dll");
    WriteLoadResult(L"engine.dll", g_engine);

    g_steam = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\bin\\steam_api.dll");
    WriteLoadResult(L"steam_api.dll", g_steam);
    if (g_steam)
    {
        auto steamInit = reinterpret_cast<SteamInitFn>(GetProcAddress(g_steam, "SteamAPI_Init"));
        if (steamInit)
            WriteText(steamInit() ? "SteamAPI_Init=true\n" : "SteamAPI_Init=false\n");
    }

    g_client = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\left4dead2\\bin\\client.dll");
    WriteLoadResult(L"client.dll", g_client);
    g_server = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\left4dead2\\bin\\server.dll");
    WriteLoadResult(L"server.dll", g_server);
    g_matchmaking = LoadGameModule(L"D:\\Steam\\steamapps\\common\\Left 4 Dead 2\\left4dead2\\bin\\matchmaking.dll");
    WriteLoadResult(L"matchmaking.dll", g_matchmaking);

    g_vstdlib_factory = GetFactory(g_vstdlib);
    g_filesystem_factory = GetFactory(g_filesystem);
    g_vgui2_factory = GetFactory(g_vgui2);
    g_engine_factory = GetFactory(g_engine);
    g_client_factory = GetFactory(g_client);
    g_server_factory = GetFactory(g_server);

    auto framework = GetMatchFramework();
    if (!framework)
        return 2;

    auto vtable = *reinterpret_cast<void ***>(framework);
    auto connect = reinterpret_cast<ConnectFn>(vtable[0]);
    bool connected = false;
    connected = connect(framework, AggregatedFactory);
    WriteText(connected ? "Connect=true\n" : "Connect=false\n");

    auto init = reinterpret_cast<InitFn>(vtable[3]);
    int initResult = -1;
    initResult = init(framework);
    WriteText("Init result=");
    WriteDecimal(static_cast<unsigned long>(initResult));
    WriteText("\n");

    auto getSession = reinterpret_cast<GetNoArgFn>(vtable[9]);
    auto session = getSession(framework);
    WriteText("GetMatchSession pointer=");
    WriteHex(reinterpret_cast<unsigned long>(session));
    WriteText("\n");

    auto settings = BuildSessionSettings();
    if (!settings)
        return 3;
    WriteText("Input session settings:\n");
    DumpKeyValues(settings, 0);

    auto createSession = reinterpret_cast<CreateSessionFn>(vtable[12]);
    createSession(framework, settings);
    WriteText("CreateSession called=true\n");
    session = getSession(framework);
    WriteText("GetMatchSession after CreateSession pointer=");
    WriteHex(reinterpret_cast<unsigned long>(session));
    WriteText("\n");
    if (session)
        DumpMatchSession(session);

    auto steamRunCallbacks = reinterpret_cast<SteamRunCallbacksFn>(GetProcAddress(g_steam, "SteamAPI_RunCallbacks"));
    if (steamRunCallbacks)
    {
        WriteText("SteamAPI_RunCallbacks begin\n");
        steamRunCallbacks();
        WriteText("SteamAPI_RunCallbacks end\n");
    }

    if (session)
    {
        WriteText("Session snapshot after callbacks:\n");
        DumpMatchSession(session);
    }

    QuerySteamLobbies();

    WriteText("RunFrame skipped=true reason=external-process-no-engine-frame\n");
    session = getSession(framework);
    WriteText("GetMatchSession pointer=");
    WriteHex(reinterpret_cast<unsigned long>(session));
    WriteText("\n");
    return 0;
}

extern "C" void __stdcall WinMainCRTStartup()
{
    ExitProcess(static_cast<UINT>(Run()));
}
