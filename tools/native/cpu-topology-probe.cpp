// Read CPUID per logical processor in this diagnostic process only.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cpuid.h>
#include <iostream>
int main(){
    DWORD_PTR allowed=0,system=0;
    if(!GetProcessAffinityMask(GetCurrentProcess(),&allowed,&system))return 2;
    std::cout<<"[";bool first=true;
    for(unsigned i=0;i<sizeof(DWORD_PTR)*8;i++){
        DWORD_PTR mask=DWORD_PTR(1)<<i;if(!(allowed&mask))continue;
        DWORD_PTR previous=SetThreadAffinityMask(GetCurrentThread(),mask);if(!previous)continue;
        unsigned a=0,b=0,c=0,d=0;__cpuid_count(0xB,0,a,b,c,d);unsigned apic=d;
        __cpuid_count(0x1A,0,a,b,c,d);unsigned type=a>>24;
        SetThreadAffinityMask(GetCurrentThread(),previous);
        if(!first)std::cout<<",";first=false;
        std::cout<<"{\"logical_processor\":"<<i<<",\"apic_id\":"<<apic<<",\"core_type_leaf_1a\":"<<type<<"}";
    }
    std::cout<<"]\n";return 0;
}
