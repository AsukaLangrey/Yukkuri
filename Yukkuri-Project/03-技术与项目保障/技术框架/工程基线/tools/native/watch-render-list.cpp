// Diagnostic only: break/watchpoints in our own child, never a system debugger.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <dbghelp.h>
#include <filesystem>
#include <fstream>
#include <map>
#include <vector>
#include <chrono>
#include <iomanip>
namespace fs=std::filesystem;
static bool read(HANDLE p,ULONG64 a,void* v,SIZE_T n){SIZE_T got=0;return ReadProcessMemory(p,(void*)a,v,n,&got)&&got==n;}
static void bytes(std::ofstream& o,HANDLE p,const char* label,ULONG64 a,SIZE_T n){
    std::vector<unsigned char>b(n);SIZE_T got=0;ReadProcessMemory(p,(void*)a,b.data(),n,&got);
    o<<label<<" address=0x"<<std::hex<<a<<" bytes=";
    for(SIZE_T i=0;i<got;i++)o<<std::setw(2)<<std::setfill('0')<<(unsigned)b[i];o<<std::dec<<"\n";
}
int wmain(int argc,wchar_t**argv){
    if(argc!=5&&argc!=6)return 2;fs::path exe=fs::absolute(argv[1]),out=fs::absolute(argv[2]);
    bool continuous=argc==6&&std::wstring(argv[5])==L"continuous";
    int limit=_wtoi(argv[3]);if(limit<1||limit>7320)return 2;fs::create_directories(out);
    SECURITY_ATTRIBUTES sa{sizeof(sa),nullptr,TRUE};
    HANDLE output=CreateFileW((out/L"stdout.log").c_str(),GENERIC_WRITE,FILE_SHARE_READ,&sa,CREATE_ALWAYS,0,nullptr);
    HANDLE error=CreateFileW((out/L"stderr.log").c_str(),GENERIC_WRITE,FILE_SHARE_READ,&sa,CREATE_ALWAYS,0,nullptr);
    HANDLE input=CreateFileW(L"NUL",GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE,&sa,OPEN_EXISTING,0,nullptr);
    STARTUPINFOW si{};si.cb=sizeof(si);si.dwFlags=STARTF_USESHOWWINDOW|STARTF_USESTDHANDLES;si.wShowWindow=SW_HIDE;
    si.hStdOutput=output;si.hStdError=error;si.hStdInput=input;PROCESS_INFORMATION pi{};
    std::wstring command=L"\""+exe.wstring()+L"\" "+argv[4];
    if(!CreateProcessW(exe.c_str(),command.data(),nullptr,nullptr,TRUE,DEBUG_ONLY_THIS_PROCESS,nullptr,exe.parent_path().c_str(),&si,&pi))return 3;
    CloseHandle(input);CloseHandle(output);CloseHandle(error);
    std::ofstream log(out/"watch-events.txt");log<<"own_child_pid="<<pi.dwProcessId<<"\n";
    std::map<DWORD,HANDLE> threads;ULONG64 base=0,bp=0,field=0,list=0;bool captured=false,failed=false,timedOut=false;
    auto start=std::chrono::steady_clock::now();uint64_t frames=0,writes=0;
    auto configure=[&](){
        for(auto [id,h]:threads){CONTEXT c{};c.ContextFlags=CONTEXT_DEBUG_REGISTERS;
            if(!GetThreadContext(h,&c))continue;
            c.Dr0=list;c.Dr1=field;c.Dr6=0;c.Dr7=(list?0x90001:0)|(field?0x900004:0);
            if(!SetThreadContext(h,&c)){log<<"watch_setup_error="<<GetLastError()<<" thread="<<id<<"\n";failed=true;}
        }
    };
    auto snapshot=[&](DWORD tid,CONTEXT& c,EXCEPTION_RECORD record,const char* reason){
        captured=true;log<<"capture_reason="<<reason<<" frames="<<frames<<" writes="<<writes<<" elapsed_seconds="<<std::chrono::duration<double>(std::chrono::steady_clock::now()-start).count()<<"\n";
        log<<std::hex<<"base=0x"<<base<<" rip=0x"<<c.Rip<<" rax=0x"<<c.Rax<<" rbx=0x"<<c.Rbx<<" rcx=0x"<<c.Rcx<<" rdx=0x"<<c.Rdx<<" rsi=0x"<<c.Rsi<<" rdi=0x"<<c.Rdi<<" r12=0x"<<c.R12<<" r14=0x"<<c.R14<<" rsp=0x"<<c.Rsp<<" field=0x"<<field<<" list=0x"<<list<<std::dec<<"\n";
        bytes(log,pi.hProcess,"writer_code",c.Rip-32,64);bytes(log,pi.hProcess,"list",list,128);bytes(log,pi.hProcess,"stack",c.Rsp,2048);
        EXCEPTION_POINTERS ep{&record,&c};MINIDUMP_EXCEPTION_INFORMATION ei{tid,&ep,FALSE};
        HANDLE dump=CreateFileW((out/L"writer.dmp").c_str(),GENERIC_WRITE,0,nullptr,CREATE_ALWAYS,0,nullptr);
        BOOL saved=MiniDumpWriteDump(pi.hProcess,pi.dwProcessId,dump,(MINIDUMP_TYPE)(MiniDumpWithFullMemory|MiniDumpIgnoreInaccessibleMemory|MiniDumpWithThreadInfo|MiniDumpWithFullMemoryInfo),&ei,nullptr,nullptr);
        log<<"dump_saved="<<saved<<" error="<<(saved?0:GetLastError())<<"\n";CloseHandle(dump);log.flush();
    };
    DWORD exitCode=0;
    for(;;){DEBUG_EVENT e{};
        if(!timedOut&&std::chrono::duration<double>(std::chrono::steady_clock::now()-start).count()>limit){timedOut=true;TerminateProcess(pi.hProcess,1460);}
        if(!WaitForDebugEvent(&e,1000)){
            if(!timedOut&&std::chrono::duration<double>(std::chrono::steady_clock::now()-start).count()>limit){timedOut=true;TerminateProcess(pi.hProcess,1460);}continue;
        }
        DWORD disposition=DBG_CONTINUE;bool done=false;
        if(e.dwDebugEventCode==CREATE_PROCESS_DEBUG_EVENT){
            base=(ULONG64)e.u.CreateProcessInfo.lpBaseOfImage;bp=base+0x14200ed;
            threads[e.dwThreadId]=e.u.CreateProcessInfo.hThread;
            unsigned char code[7],expected[]={0x4d,0x8b,0xa6,0x70,0x09,0,0};
            if(!read(pi.hProcess,bp,code,7)||memcmp(code,expected,7)!=0){log<<"unsupported_instruction_bytes=1\n";failed=true;}
            else{unsigned char trap=0xcc;SIZE_T n=0;failed=!WriteProcessMemory(pi.hProcess,(void*)bp,&trap,1,&n)||n!=1;FlushInstructionCache(pi.hProcess,(void*)bp,1);}
            log<<std::hex<<"breakpoint=0x"<<bp<<std::dec<<" installed="<<!failed<<"\n";
            if(e.u.CreateProcessInfo.hFile)CloseHandle(e.u.CreateProcessInfo.hFile);
            if(e.u.CreateProcessInfo.hProcess!=pi.hProcess)CloseHandle(e.u.CreateProcessInfo.hProcess);
        }else if(e.dwDebugEventCode==CREATE_THREAD_DEBUG_EVENT){threads[e.dwThreadId]=e.u.CreateThread.hThread;configure();}
        else if(e.dwDebugEventCode==EXIT_THREAD_DEBUG_EVENT){auto t=threads.find(e.dwThreadId);if(t!=threads.end()){CloseHandle(t->second);threads.erase(t);}}
        else if(e.dwDebugEventCode==LOAD_DLL_DEBUG_EVENT){if(e.u.LoadDll.hFile)CloseHandle(e.u.LoadDll.hFile);}
        else if(e.dwDebugEventCode==EXCEPTION_DEBUG_EVENT){
            auto record=e.u.Exception.ExceptionRecord;auto t=threads.find(e.dwThreadId);
            CONTEXT c{};c.ContextFlags=CONTEXT_ALL;
            if(t==threads.end()||!GetThreadContext(t->second,&c)){failed=true;}
            else if(record.ExceptionCode==EXCEPTION_BREAKPOINT&&(ULONG64)record.ExceptionAddress==bp){
                ULONG64 next=0;bool changed=field!=c.R14+0x970;field=c.R14+0x970;
                if(!read(pi.hProcess,field,&next,8)){failed=true;}
                else{
                    changed|=next!=list;list=next;
                    if(continuous){
                        // Emulate exactly the validated MOV; it does not alter flags.
                        c.R12=next;c.Rip=bp+7;
                    }else{
                        // Restore the validated instruction and execute it normally once watched.
                        // Subsequent reallocations are tracked by the hardware field watchpoint.
                        unsigned char original=0x4d;SIZE_T restored=0;
                        if(!WriteProcessMemory(pi.hProcess,(void*)bp,&original,1,&restored)||restored!=1){failed=true;}
                        FlushInstructionCache(pi.hProcess,(void*)bp,1);c.Rip=bp;
                        log<<"one_shot_breakpoint_restored="<<!failed<<"\n";
                    }
                    if(!SetThreadContext(t->second,&c))failed=true;frames++;
                    if(frames%3600==0)log<<"watch_progress frames="<<frames<<" writes="<<writes<<"\n";
                    if(changed){log<<std::hex<<"list_change field=0x"<<field<<" list=0x"<<list<<std::dec<<" frame="<<frames<<"\n";configure();}
                }
            }else if(record.ExceptionCode==EXCEPTION_SINGLE_STEP&&(c.Dr6&3)){
                if(c.Dr6&2){ULONG64 next=0;if(read(pi.hProcess,field,&next,8)&&next!=list){list=next;configure();}}
                if(c.Dr6&1){writes++;ULONG64 value=0;
                    // Match the observed low-byte duplication; allocator fill is excluded.
                    if(!captured&&read(pi.hProcess,list,&value,8)&&(value&7)&&value>0x10000&&value<0x800000000000ULL&&(value&255)==((value>>8)&255))snapshot(e.dwThreadId,c,record,"misaligned_pointer_written");
                }
                c.ContextFlags=CONTEXT_DEBUG_REGISTERS;GetThreadContext(t->second,&c);c.Dr6=0;SetThreadContext(t->second,&c);
            }else if(record.ExceptionCode==EXCEPTION_BREAKPOINT){disposition=DBG_CONTINUE;}
            else{disposition=DBG_EXCEPTION_NOT_HANDLED;if(!captured&&(record.ExceptionCode==EXCEPTION_ACCESS_VIOLATION&&((ULONG64)record.ExceptionAddress-base)==0x14201b8||!e.u.Exception.dwFirstChance))snapshot(e.dwThreadId,c,record,"exception_without_writer");}
        }else if(e.dwDebugEventCode==EXIT_PROCESS_DEBUG_EVENT){done=true;exitCode=e.u.ExitProcess.dwExitCode;log<<"exit="<<exitCode<<" captured="<<captured<<" failed="<<failed<<" timeout="<<timedOut<<" frames="<<frames<<" writes="<<writes<<"\n";}
        log.flush();if((captured||failed)&&!done)TerminateProcess(pi.hProcess,captured?77:78);
        ContinueDebugEvent(e.dwProcessId,e.dwThreadId,disposition);if(done)break;
    }
    for(auto [id,h]:threads)CloseHandle(h);CloseHandle(pi.hThread);CloseHandle(pi.hProcess);return captured?77:failed?78:timedOut?79:(int)exitCode;
}
