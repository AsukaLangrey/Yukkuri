// Small fixed-byte comparison using Godot's unmodified bundled zlib sources.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <zlib.h>
#include <fstream>
#include <iostream>
#include <iterator>
#include <vector>
#include <string>
int main(int argc,char**argv){
    if(argc!=3)return 2;int core=std::stoi(argv[2]);
    if(core<0||core>=32||!SetProcessAffinityMask(GetCurrentProcess(),DWORD_PTR(1)<<core))return 3;
    std::ifstream in(argv[1],std::ios::binary);
    std::vector<unsigned char> raw((std::istreambuf_iterator<char>(in)),{});if(raw.empty())return 4;
    std::vector<unsigned char> packed(compressBound(raw.size())),restored(raw.size()),reference;
    unsigned failures=0,different=0;
    for(int i=0;i<128;i++){
        uLongf n=packed.size(),r=restored.size();
        int enc=compress2(packed.data(),&n,raw.data(),raw.size(),6);
        int dec=uncompress(restored.data(),&r,packed.data(),n);
        if(enc!=Z_OK||dec!=Z_OK||r!=raw.size()||restored!=raw){failures++;std::cerr<<"failed iteration="<<i<<" enc="<<enc<<" dec="<<dec<<"\n";}
        std::vector<unsigned char> current(packed.begin(),packed.begin()+n);
        if(i==0)reference=current;else if(current!=reference)different++;
    }
    std::cout<<"{\"logical_processor\":"<<core<<",\"iterations\":128,\"failed_roundtrips\":"<<failures<<",\"different_encodings\":"<<different<<",\"zlib\":\""<<zlibVersion()<<"\"}\n";
    return failures?1:0;
}
