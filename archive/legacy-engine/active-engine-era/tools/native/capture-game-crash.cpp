// Diagnose only the child process launched here. No registry or global debugger changes.
#define NOMINMAX
#include <windows.h>
#include <dbghelp.h>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <string>
#include <vector>
#include <chrono>

namespace fs=std::filesystem;
static std::string hex(ULONG64 n){char b[32];sprintf_s(b,"0x%llx",static_cast<unsigned long long>(n));return b;}
static void region(std::ofstream& out,HANDLE process,const char* label,ULONG64 address){
    MEMORY_BASIC_INFORMATION info{};
    SIZE_T count=VirtualQueryEx(process,reinterpret_cast<void*>(address),&info,sizeof(info));
    out<<label<<" address="<<hex(address)<<" queried="<<count<<" base="<<hex(reinterpret_cast<ULONG64>(info.BaseAddress))
       <<" allocation="<<hex(reinterpret_cast<ULONG64>(info.AllocationBase))<<" size="<<info.RegionSize
       <<" state="<<hex(info.State)<<" protection="<<hex(info.Protect)<<" type="<<hex(info.Type)<<"\n";
}
static void memory(std::ofstream& out,HANDLE process,const char* label,ULONG64 address,SIZE_T length){
    std::vector<unsigned char> bytes(length);SIZE_T read=0;
    BOOL ok=ReadProcessMemory(process,reinterpret_cast<void*>(address),bytes.data(),length,&read);
    DWORD error=ok?0:GetLastError();
    out<<label<<" address="<<hex(address)<<" read="<<read<<" error="<<error<<" bytes=";
    for(SIZE_T i=0;i<read;i++)out<<std::hex<<std::setw(2)<<std::setfill('0')<<static_cast<unsigned>(bytes[i]);
    out<<std::dec<<"\n";
}
int wmain(int argc,wchar_t** argv){
    if(argc<5)return 2;
    fs::path executable=fs::absolute(argv[1]),output=fs::absolute(argv[2]);
    // Zero means observe until exit, with no forced timeout termination.
    int seconds=_wtoi(argv[3]);if(seconds<0||seconds>7320)return 2;
    fs::create_directories(output);
    std::wstring command=L"\""+executable.wstring()+L"\" "+argv[4];
    SECURITY_ATTRIBUTES security{sizeof(SECURITY_ATTRIBUTES),nullptr,TRUE};
    HANDLE stdoutFile=CreateFileW((output/L"stdout.log").c_str(),GENERIC_WRITE,FILE_SHARE_READ,&security,CREATE_ALWAYS,0,nullptr);
    HANDLE stderrFile=CreateFileW((output/L"stderr.log").c_str(),GENERIC_WRITE,FILE_SHARE_READ,&security,CREATE_ALWAYS,0,nullptr);
    HANDLE input=CreateFileW(L"NUL",GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE,&security,OPEN_EXISTING,0,nullptr);
    STARTUPINFOW start{};start.cb=sizeof(start);start.dwFlags=STARTF_USESHOWWINDOW|STARTF_USESTDHANDLES;
    // The observer may run hidden, but its explicitly requested interactive game
    // must be visible. Hiding the game changes the reproduction conditions.
    start.wShowWindow=SW_SHOWNORMAL;start.hStdOutput=stdoutFile;start.hStdError=stderrFile;start.hStdInput=input;
    PROCESS_INFORMATION process{};
    std::ofstream events(output/"debug-events.txt");events<<"capture_version=4\nstartup_show_window=SW_SHOWNORMAL\n";
    if(stdoutFile==INVALID_HANDLE_VALUE||stderrFile==INVALID_HANDLE_VALUE||input==INVALID_HANDLE_VALUE){
        events<<"log_handle_error="<<GetLastError()<<"\n";return 3;
    }
    if(!CreateProcessW(executable.c_str(),command.data(),nullptr,nullptr,TRUE,DEBUG_ONLY_THIS_PROCESS,nullptr,executable.parent_path().c_str(),&start,&process)){
        events<<"create_error="<<GetLastError()<<"\n";return 3;
    }
    // Losing the observer must not kill the game and masquerade as a crash.
    if(!DebugSetProcessKillOnExit(FALSE))events<<"debug_kill_policy_error="<<GetLastError()<<"\n";
    CloseHandle(stdoutFile);CloseHandle(stderrFile);CloseHandle(input);
    events<<"pid="<<process.dwProcessId<<"\n";events.flush();
    auto began=std::chrono::steady_clock::now();ULONG64 base=0;bool captured=false,timedOut=false,initialBreakpoint=false;DWORD result=0;
    for(;;){
        DEBUG_EVENT e{};
        if(!WaitForDebugEvent(&e,1000)){
            if(seconds>0&&std::chrono::duration<double>(std::chrono::steady_clock::now()-began).count()>seconds&&!timedOut){
                timedOut=true;events<<"timeout_terminate_own_child=1\n";TerminateProcess(process.hProcess,1460);
            }
            continue;
        }
        DWORD disposition=DBG_CONTINUE;bool finished=false;
        if(e.dwDebugEventCode==CREATE_PROCESS_DEBUG_EVENT){
            base=reinterpret_cast<ULONG64>(e.u.CreateProcessInfo.lpBaseOfImage);
            events<<"main_base="<<hex(base)<<"\n";
            if(e.u.CreateProcessInfo.hFile)CloseHandle(e.u.CreateProcessInfo.hFile);
            if(e.u.CreateProcessInfo.hThread&&e.u.CreateProcessInfo.hThread!=process.hThread)CloseHandle(e.u.CreateProcessInfo.hThread);
            if(e.u.CreateProcessInfo.hProcess&&e.u.CreateProcessInfo.hProcess!=process.hProcess)CloseHandle(e.u.CreateProcessInfo.hProcess);
        }else if(e.dwDebugEventCode==CREATE_THREAD_DEBUG_EVENT){
            CloseHandle(e.u.CreateThread.hThread);
        }else if(e.dwDebugEventCode==LOAD_DLL_DEBUG_EVENT){
            if(e.u.LoadDll.hFile)CloseHandle(e.u.LoadDll.hFile);
        }else if(e.dwDebugEventCode==EXCEPTION_DEBUG_EVENT){
            auto& exception=e.u.Exception;auto record=exception.ExceptionRecord;
            ULONG64 address=reinterpret_cast<ULONG64>(record.ExceptionAddress),offset=address-base;
            bool startupBreakpoint=record.ExceptionCode==EXCEPTION_BREAKPOINT&&exception.dwFirstChance&&!initialBreakpoint;
            if(startupBreakpoint)initialBreakpoint=true;
            disposition=startupBreakpoint?DBG_CONTINUE:DBG_EXCEPTION_NOT_HANDLED;
            if(!startupBreakpoint)events<<"exception code="<<hex(record.ExceptionCode)<<" address="<<hex(address)
                <<" thread="<<e.dwThreadId<<" first_chance="<<exception.dwFirstChance<<"\n";
            // No build-specific offsets: handled first-chance CLR exceptions are
            // not crashes, and old official-engine offsets do not identify art03.
            if(!captured&&!exception.dwFirstChance){
                captured=true;events<<"captured code="<<hex(record.ExceptionCode)<<" address="<<hex(address)
                    <<" module_offset="<<hex(offset)<<" first_chance="<<exception.dwFirstChance
                    <<" thread="<<e.dwThreadId
                    <<" elapsed_seconds="<<std::chrono::duration<double>(std::chrono::steady_clock::now()-began).count()<<"\n";
                HANDLE thread=OpenThread(THREAD_GET_CONTEXT|THREAD_QUERY_INFORMATION,FALSE,e.dwThreadId);
                CONTEXT context{};context.ContextFlags=CONTEXT_ALL;
                BOOL got=GetThreadContext(thread,&context);
                events<<"context_ok="<<got<<" error="<<(got?0:GetLastError())<<"\n";
                if(got){
                    events<<"rip="<<hex(context.Rip)<<" rax="<<hex(context.Rax)<<" rbx="<<hex(context.Rbx)
                       <<" rcx="<<hex(context.Rcx)<<" rdx="<<hex(context.Rdx)<<" rsi="<<hex(context.Rsi)
                       <<" rdi="<<hex(context.Rdi)<<" rsp="<<hex(context.Rsp)<<" r12="<<hex(context.R12)<<"\n";
                    for(DWORD i=0;i<record.NumberParameters;i++)events<<"exception_parameter_"<<i<<"="<<hex(record.ExceptionInformation[i])<<"\n";
                    region(events,process.hProcess,"rbx_region",context.Rbx);region(events,process.hProcess,"rsi_region",context.Rsi);
                    memory(events,process.hProcess,"rbx_memory",context.Rbx,128);memory(events,process.hProcess,"rsi_memory",context.Rsi,256);
                    if(context.Rbx>=64)memory(events,process.hProcess,"rbx_neighborhood",(context.Rbx&~ULONG64(15))-64,384);
                    if(context.R12>=64)memory(events,process.hProcess,"r12_neighborhood",context.R12-64,256);
                    memory(events,process.hProcess,"stack",context.Rsp,1024);
                    EXCEPTION_POINTERS pointers{&record,&context};MINIDUMP_EXCEPTION_INFORMATION info{e.dwThreadId,&pointers,FALSE};
                    HANDLE dump=CreateFileW((output/L"crash.dmp").c_str(),GENERIC_WRITE,0,nullptr,CREATE_ALWAYS,0,nullptr);
                    auto flags=static_cast<MINIDUMP_TYPE>(MiniDumpWithFullMemory|MiniDumpIgnoreInaccessibleMemory|MiniDumpWithFullMemoryInfo|MiniDumpWithThreadInfo|MiniDumpWithUnloadedModules);
                    BOOL saved=MiniDumpWriteDump(process.hProcess,e.dwProcessId,dump,flags,&info,nullptr,nullptr);
                    events<<"dump_saved="<<saved<<" error="<<(saved?0:GetLastError())<<"\n";CloseHandle(dump);
                }
                if(thread)CloseHandle(thread);
            }
        }else if(e.dwDebugEventCode==EXIT_PROCESS_DEBUG_EVENT){
            result=e.u.ExitProcess.dwExitCode;finished=true;
            events<<"exit_code="<<hex(result)<<" elapsed_seconds="<<std::chrono::duration<double>(std::chrono::steady_clock::now()-began).count()
               <<" captured="<<captured<<" timed_out="<<timedOut<<"\n";
        }
        events.flush();ContinueDebugEvent(e.dwProcessId,e.dwThreadId,disposition);
        if(finished)break;
    }
    CloseHandle(process.hThread);CloseHandle(process.hProcess);
    return result==0&&!timedOut?0:1;
}
