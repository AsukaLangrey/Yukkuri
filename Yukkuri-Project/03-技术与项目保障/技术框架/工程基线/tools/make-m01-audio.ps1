. "$PSScriptRoot/env.ps1"
# Deterministic nonverbal reaction sounds. No samples, copied melodies, or speech recordings.
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class M01AudioBake {
  public static void Write(string path, int kind) {
    int rate=22050; double seconds=kind==5?1.1:.62; int n=(int)(rate*seconds);
    using(var stream=File.Create(path)) using(var b=new BinaryWriter(stream)) {
      b.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));b.Write(36+n*2);
      b.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));b.Write(16);b.Write((short)1);b.Write((short)1);
      b.Write(rate);b.Write(rate*2);b.Write((short)2);b.Write((short)16);
      b.Write(System.Text.Encoding.ASCII.GetBytes("data"));b.Write(n*2);
      double phase=0;
      for(int i=0;i<n;i++){
        double t=(double)i/rate,u=t/seconds;
        double baseHz=kind==0?280+60*Math.Sin(u*Math.PI):
                      kind==1?310+150*u:
                      kind==2?360+60*Math.Sin(t*38):
                      kind==3?650-310*u:
                      kind==4?370-100*u:420+100*Math.Sin(t*14);
        phase+=2*Math.PI*baseHz/rate;
        double env=Math.Sin(Math.PI*u)*Math.Min(1,t/.035);
        if(kind==2)env*=.35+.65*Math.Pow(Math.Sin(t*18),2);
        if(kind==5)env*=Math.Pow(Math.Sin(t*9),2);
        double sample=(Math.Sin(phase)+.22*Math.Sin(phase*2)+.10*Math.Sin(phase*3))*env*.22;
        b.Write((short)(sample*32767));
      }
    }
  }
}
'@
$target=Join-Path $GameRoot 'Assets/Audio'
$names=@('coo-stroke','coo-hug','coo-nuzzle','coo-hurt','coo-refuse','coo-help')
$records=@()
for($i=0;$i -lt $names.Count;$i++){
    $file=Join-Path $target ($names[$i]+'.wav')
    [M01AudioBake]::Write($file,$i)
    $records+= [ordered]@{file=$names[$i]+'.wav';sha256=(Get-FileHash $file -Algorithm SHA256).Hash;sample_rate=22050;channels=1;bit_depth=16;source='tools/make-m01-audio.ps1';license='Project-generated original nonverbal synthesis; no external samples';role='M01 reaction cue, not a licensed character voice or speech synthesis'}
}
$records|ConvertTo-Json -Depth 5|Set-Content (Join-Path $target 'm01-audio-analysis.json')
$manifestPath=Join-Path $ProjectRoot 'assets-source/asset-manifest.json'
$manifest=Get-Content $manifestPath -Raw|ConvertFrom-Json
$manifest.assets=@($manifest.assets|Where-Object {$_.source -ne 'tools/make-m01-audio.ps1'})
foreach($r in $records){
    $manifest.assets+=[pscustomobject]@{path='src/Yukkuri.Game/Assets/Audio/'+$r.file;sha256=$r.sha256;bytes=(Get-Item (Join-Path $target $r.file)).Length;recorded=[DateTimeOffset]::Now.ToString('yyyy-MM-dd');status='milestone-prototype';author='Project-generated original synthesis';source=$r.source;license=$r.license;modifications='None';use=$r.role}
}
$manifest|ConvertTo-Json -Depth 12|Set-Content $manifestPath
Write-Output "Generated and catalogued $($records.Count) M01 audio cues."
