using Yukkuri.Simulation;

// Remote-only descriptor checks. These do not prove Godot layout, animation or audio.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
Check("保存期内的说明与真实十倍延缓一致",()=>
{
    var c=new Creature();c.Family.ColdStorage.Stored=true;
    c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds-1;
    Require(WorldSimulation.BiologicalTimeScale(c)==.1,"保存期内实际延缓倍率不符");
    string text=FamilyRules.Describe(c);
    Require(text.Contains("十倍延缓")&&!text.Contains("已超时"),"有效保存被描述成失效");
});
Check("超期但仍Stored时说明不能声称继续延缓",()=>
{
    foreach(double extra in new[]{0d,10d,100d})
    {
        var c=new Creature();c.Family.ColdStorage.Stored=true;
        c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds+extra;
        Require(WorldSimulation.BiologicalTimeScale(c)==1,"超期状态意外继续延缓");
        string text=FamilyRules.Describe(c);
        Require(text.Contains("延缓已结束")&&text.Contains("取出")&&!text.Contains("按十倍延缓"),"超期占位仍被显示成有效保存");
    }
});
Check("取出后旧超期计时不能盖过实际复苏状态",()=>
{
    var c=new Creature();c.Family.ColdStorage.Elapsed=WorldSimulation.ColdStorageLimitSeconds+10;
    c.Family.ColdStorage.Stored=false;c.Family.ColdStorage.RecoveryRemaining=20;
    string text=FamilyRules.Describe(c);
    Require(text.Contains("取出冷藏后正在缓慢恢复")&&!text.Contains("冷藏已超时")&&!text.Contains("按十倍延缓"),"取出后的旧计时造成错误保存提示");
});
Environment.ExitCode=failures==0?0:1;
