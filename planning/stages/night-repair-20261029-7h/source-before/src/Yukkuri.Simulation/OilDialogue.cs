namespace Yukkuri.Simulation;

// Original short lines: event-driven and localized; no external model or source text at runtime.
public static class OilDialogue
{
    public static string Say(Creature c, string situation)
    {
        string name = c.Name;
        string accessory = c.Body.SpeciesId == "marisa" ? "帽子先生" : "蝴蝶结先生";
        var lines = situation switch
        {
            "thirsty" => new[] { "嘴嘴干干的……给油一口水，好不好？", "水水先生，来嘴边这里……", "想慢慢喝一口水啦……" },
            "not-thirsty" => new[] { "水水已经喝好啦，先不用哦。", "油现在不渴，杯杯先放旁边吧。" },
            "suspicious-water" => new[] { "这次真的给油喝吗……", "杯杯不要又拿走哦……油先看看。" },
            "accessory-care-wait" => new[] { $"{accessory}要轻轻的哦……", "油在这里等，慢慢整理吧。", "整理好了，要戴回来哦。" },
            "greet" => new[] { $"{name}在这里！慢慢来呀～", "今天也要慢慢来！", "是来找油玩的吗？" },
            "greet-rest" => new[] { "嗯……油听见啦。再歇一小会儿哦……", "在这里呢……一起慢慢歇歇吧。" },
            "praise-rest" => new[] { "嘿嘿……有听见哦。油再歇一会儿……", "嗯……暖暖的话话。慢慢来……" },
            "comfort-rest" => new[] { "还在旁边呢……油可以慢慢歇啦。", "嗯……陪油静静待一会儿吧……" },
            "comfort-distance" => new[] { "先在那里陪油，好不好……", "不要再靠近啦……油还怕怕的。" },
            "greet-shy" => new[] { "是、是在叫油吗……", "先站在那里，好不好？", "慢慢过来……" },
            "praise" => new[] { $"{name}做到了！", "嘿嘿，下次也会慢慢做好的！", "有好好看着油呢！" },
            "praise-empty" => new[] { "诶嘿嘿……要摸摸吗？", "油有被好好看着呢～", "一起慢慢来呀！" },
            "comfort" => new[] { "再陪油一小会儿……", "这里可以慢慢来吗？", "嗯……先待在这里。" },
            "touch-no" => new[] { "现在不要碰……", "那里不可以慢慢摸！", "先不要过来，好不好……" },
            "touch-rough" => new[] { "摸摸不是这样子的！轻一点呀！", "手手太用力啦！先放开油！", "不要急急地揉呀，油不能慢慢来了！" },
            "nuzzle-rough" => new[] { "蹭蹭不是这样子的！轻轻的才好呀！", "脸脸不要这样揉！先停一下！", "不能慢慢蹭了……放开油呀！" },
            "rock-rough" => new[] { "晃晃太急啦！停下来呀！", "油要晕晕了……慢慢放稳！", "被被不要再晃啦，先停一下！" },
            "touch-sore" => new[] { "这里还痛痛……先不要揉。", "轻一点呀，这边还不能慢慢来。", "先帮油看看这里，好不好……" },
            "stroke" => new[] { "摸摸～慢慢摸～", "手手暖暖的呢……", "这里，好舒服呀～" },
            "stroke-more" => new[] { "再摸摸这里！", "摸摸先生不要走嘛～", "油还想慢慢来！" },
            "nuzzle" => new[] { "蹭蹭～一起蹭蹭～", "脸脸也要慢慢来！", "再慢慢蹭一下～" },
            "nuzzle-shy" => new[] { "先……轻轻蹭一下。", "靠过来也可以吗？", "只有一点点哦……" },
            "nuzzle-ready" => new[] { "脸脸靠过来啦……一起慢慢蹭蹭吧。", "油也想和你蹭蹭～轻轻的哦。", "这里可以慢慢来，蹭蹭吧～" },
            "lift" => new[] { "托稳油哦～", "高高的！慢慢来～", "带油去那边吧！" },
            "lift-fear" => new[] { "不要丢下油！", "太高了……放油下去！", "手手别松开……" },
            "lift-high" => new[] { "高高不要啦……油想低一点！", "下面好远……手手托住，慢慢放低呀！", "不要往上了，好不好……油不能慢慢来啦。" },
            "lift-low" => new[] { "低一点啦……手手还要托稳哦。", "嗯……慢慢降下来就好。不要突然松开哦。", "地面近一些了……油先看看。" },
            "down" => new[] { "脚脚碰到地面啦！", "这里可以慢慢来～", "放得稳稳的呢。" },
            "lowering" => new[] { "手手托住啦……慢慢放低哦。", "绳绳解开啦……先不要松开手呀。" },
            "wrap" => new[] { "被被软软的～", "包好啦，可以慢慢来！", "暖暖的呢……" },
            "rock" => new[] { "摇呀摇～慢慢摇～", "再来一下，好不好？", "困困了……" },
            "unwrap" => new[] { "油出来啦～", "伸伸～可以动啦！", "下次还要被被先生。" },
            "hungry" => new[] { "饭饭先生……到这边来！", "肚肚空空的……", "油想慢慢吃饭饭。" },
            "eat" => new[] { "嚼嚼……饭饭真好～", "吃完这一口，再慢慢说！", "幸福～饭饭暖暖的！" },
            "sweet" => new[] { "甜甜！幸福～！", "甜甜要慢慢吃！", "留一点甜甜，慢慢吃～" },
            "drink" => new[] { "水水，小口喝～", "咕嘟……舒服一点啦。", "还要慢慢喝一点。" },
            "full" => new[] { "肚肚已经圆圆的啦！", "这一口先不要了……", "饭饭等一会儿，好不好？" },
            "suspicious-food" => new[] { "这次不会又拿走吧……", "放在这里，油自己吃。", "甜甜真的给油吗？" },
            "clean" => new[] { "脏脏走开～", "轻轻擦这里，好不好？", "擦干净就能慢慢来啦。" },
            "dry" => new[] { "脚脚干干的～", "毛巾先生，慢慢擦。", "湿湿没有啦！" },
            "drying" => new[] { "这里湿湿的……毛巾先生，慢慢来。", "轻轻擦这里哦，还要一点点。", "油会等着的，慢慢擦～" },
            "dry-face" => new[] { "脸脸这边干干啦～", "湿湿走掉啦！再看看另一边哦。", "这边不湿了，轻轻擦就好。" },
            "dry-body" => new[] { "这边干干的，舒服一点啦。", "湿湿走掉了……慢慢来～", "毛巾先生，谢谢啦！" },
            "clean-done" => new[] { "这边的脏脏不见啦！", "干净一点啦，慢慢来～", "还有别的脏脏吗？轻轻看看哦。" },
            "sun-bottom" => new[] { "脚脚也要晒晒～", "翻过来，慢慢晾一下！", "干干的才舒服呢。" },
            "accessory-care" => new[] { $"要把{accessory}还给油哦。", "要一直看得见它哦！", "轻轻整理，拜托啦。" },
            "accessory-return" => new[] { $"{accessory}回来啦！", "戴好啦，慢慢来～", "它还是油的宝贝呢。" },
            "accessory-wary" => new[] { "这次不要再拿走了……", $"{accessory}，待在油这里。", "油会好好看着它。" },
            "accessory-taken" => new[] { $"{accessory}是{name}的！", "快还回来！不许拿走！", "那个不能给你！" },
            "accessory-high" => new[] { $"{accessory}，下来呀！", "再跳一下……够不到……", "为什么放到那里去！" },
            "accessory-broken" => new[] { $"{accessory}……坏掉了……", "油的宝贝……还给油……", "不要再弄它了！" },
            "repair" => new[] { "这里也能补好吗？", "慢慢修，油在这里等。", "宝贝先生，要好起来呀。" },
            "repair-done" => new[] { $"{accessory}补好啦……还是油原来的宝贝呢。", "细细的线线……以后要好好珍惜哦。", "可以戴回来了吗？油想慢慢试试。" },
            "accessory-clean" => new[] { $"{accessory}干净啦！轻轻戴回来吧。", "脏脏不见了……油有一直看着呢。" },
            "new-accessory" => new[] { "新的……也给油吗？", "油会好好戴着的。", "以前那个，油还记得……" },
            "ball" => new[] { "球球过来啦！", "这次轮到油！", "嘿咻！球球慢慢滚～" },
            "treasure" => new[] { "宝物先生！亮亮的！", "这个是油的啦～", "给你看看油的宝物！" },
            "treasure-missing" => new[] { "石头先生被拿走啦……要还给油哦。", "油的小宝物呢……还想再看看。" },
            "treasure-return" => new[] { "是油的石头先生……回来啦。", "还认得这个亮亮的……下次先和油说哦。" },
            "sing" => new[] { "慢～慢～来～呀～♪", "油油～一起慢慢来～♪", "饭饭香香～慢慢来～♪" },
            "stretch" => new[] { "伸～伸～懒腰～", "长长～再慢慢缩回来！", "身体也要慢慢来～" },
            "learn" => new[] { "油看着呢……这样做？", "再慢慢做一遍，好不好？", "先跟着手手试一下！" },
            "learn-failed" => new[] { "油还没弄明白……", "不是这样吗？再告诉油。", "那个手势……是什么意思？" },
            "learn-success" => new[] { "做到啦！有看见吗？", "这次油记住了！", "慢慢做，也能做到呢！" },
            "wait" => new[] { "等一等……先不吃。", "饭饭先生，等油一下哦。", "油在好好等着呢！" },
            "follow" => new[] { "慢慢跟上去～", "等等油的小脚脚！", "油要跟你去那边！" },
            "stop" => new[] { "停住啦！", "这里等着，对不对？", "脚脚先不要动哦。" },
            "inspect" => new[] { "这里有一点痛痛……", "看看就好，轻一点哦。", "油是不是能好起来？" },
            "care" => new[] { "包好以后，慢慢休息……", "轻一点……油会等着。", "谢谢……这里没那么痛啦。" },
            "rest" => new[] { "小窝先生，油回来啦。", "先休息一小会儿……", "这里软软的，可以慢慢来。" },
            "rest-invite" => new[] { "想慢慢歇一会儿……", "小窝先生在哪里呀？", "找个稳稳的地方，油想休息啦。" },
            "poke" => new[] { "噗库！不要戳这里！", "脸脸不是球球呀！", "再戳油要生气啦！" },
            "blocked" => new[] { "让油过去呀！", "饭饭在那边，手手走开！", "这边也过不去吗？" },
            "teased" => new[] { "甜甜怎么又跑了！", "明明说要给油的……", "油不要追了！哼！" },
            "tease-unreachable" => new[] { "甜甜……油的脚脚走不过去……", "拿到嘴嘴这里，好不好？", "油想吃，可是身体动不了呀……" },
            "scold" => new[] { "油先停下……", "知道啦……不要那么大声。", "刚才那样不可以吗？" },
            "confused-scold" => new[] { "油刚才做什么了？", "为什么凶油……", "油明明在这里慢慢来。" },
            "scold-immobile" => new[] { "油已经动不了啦……不要再凶油嘛。", "油现在走不开呀……不是故意不听的。" },
            "cold-request" => new[] { "刚才的话……没有听见吗？", "油还需要帮帮呀……先等一小会儿吧。" },
            "belittle-defiant" => new[] { "噗库！不许那样说油！", "油也想好好慢慢来呀！不要随便凶油！" },
            "belittle-hurt" => new[] { "那样说……油会难过的。", "油也想慢慢来呀……不要说那样的话。" },
            "cold" => new[] { "不想和油说话吗……", "那……油先等一下。", "油只是想慢慢来……" },
            "angry" => new[] { "噗库！油生气了！", "不许这样对油！", "这里不能慢慢来了！" },
            "hurt" => new[] { "痛痛！停下来呀！", "不要再碰这里……", "让油慢慢来呀……" },
            "squeeze" => new[] { "挤挤的！放开油！", "这里不能这么压呀！", "不要再用力了！" },
            "roll" => new[] { "天和地转起来了！", "不要再滚了！", "晕晕……让油停下来！" },
            "ball-touch" => new[] { "呜哇，球球碰到油啦！", "球球，慢一点过来呀。" },
            "ball-askew" => new[] { "球球跑歪啦……再来一次嘛！", "刚才没有顶好……油再慢慢试试。" },
            "fall" => new[] { "接住油——！", "脚脚碰不到地面啦！", "不要丢下油！" },
            "flipped" => new[] { "翻过来了……油要翻回去！", "脚脚朝上啦！", "帮油转回来！" },
            "restrained" => new[] { "动不了……放开油！", "手手拿开呀！", "油想自己慢慢走！" },
            "released" => new[] { "终于松开了……", "油可以走了吗？", "先不要再碰油。" },
            "box" => new[] { "门门，打开呀！", "外面的人，听见油了吗？", "让油出去慢慢来！" },
            "box-brave" => new[] { "噗库！油要自己出去！", "门门为什么不让路！", "开门呀，油还要玩！" },
            "box-open" => new[] { "门开了……真的可以出去？", "油要慢慢出来。", "先让油看看外面……" },
            "foot-hurt" => new[] { "脚脚痛痛！不要碰脚脚！", "油的脚脚不能慢慢来了……", "停下来！油想回小窝！" },
            "cannot-walk" => new[] { "脚脚……走不动了……", "小窝太远了，抱油过去吧……", "饭饭拿到这边来，好不好？" },
            "fire-memory" => new[] { "那个！上次痛痛的那个！", "不要把那个拿过来！", "油记得……脚脚好痛的！" },
            "hang" => new[] { "脚脚够不到地面！", "放油下去，拜托了……", "有人听见油吗……" },
            "recovery" => new[] { "脚脚可以慢慢动一点了！", "再试一步……慢慢来。", "还要休息，油记得呢。" },
            "request-nuzzle" => new[] { "手手过来，蹭蹭好不好？", "油想和你蹭蹭～", "脸脸在这里哦！" },
            "request-play" => new[] { "球球呢？再一起玩吧！", "看看油的宝物呀！", "陪油慢慢玩一会儿嘛～" },
            "too-tired" => new[] { "油累累了……", "等油慢慢休息一下。", "现在不想玩了……" },
            _ => new[] { "慢慢来……", "油在这里哦。" }
        };
        // Context wins over style: a brave voice must not invent a working pair of feet.
        if(situation=="mouthful-done")
            lines=c.Interaction.EatingKind==IntakeKind.Water?
                c.Interaction.Hydration<55?["咕嘟……咽好啦。油还想小口喝一点。","水水咽下去啦……嘴嘴还想再润润。"]:["咕嘟……咽好啦，嘴嘴舒服多啦。","这一口喝好啦……慢慢来～"]:
                c.Fullness>=90?["这一口咽好啦，肚肚圆圆的。","啊呜……吃好啦，饭饭先歇一会儿吧。"]:
                c.Interaction.EatingKind==IntakeKind.Sweet?["甜甜咽好啦……幸福还在嘴嘴里呢～","啊呜……油已经咽下去啦！"]:["饭饭咽好啦……再慢慢看一看。","啊呜……这一口吃好啦，慢慢来～"];
        else if(situation=="inspect"&&c.Interaction.HeadSensitivity>15)
            lines=["额头这里还痛痛……要轻轻的哦。","额头还不能慢慢揉，先帮油看看吧……"];
        else if(situation=="inspect"&&c.Interaction.CheekSensitivity>15)
        {
            string side=c.Interaction.CheekSensitivityAt(SurfaceSide.Left)>15&&c.Interaction.CheekSensitivityAt(SurfaceSide.Right)>15?"两边的脸脸":
                c.Interaction.CheekSensitivityAt(SurfaceSide.Left)>15?"左边脸脸":"右边脸脸";
            lines=[$"{side}还不舒服……先不要揉呀。",$"{side}想歇一会儿……轻轻看看就好。"];
        }
        else if(situation=="inspect"&&c.Interaction.Hydration<25&&c.Pain<5&&c.Health>95)
            lines=["嘴嘴干干的……油想喝一口水。","水水先生，递到嘴边好不好……"];
        else if(situation=="inspect"&&c.Pain<5&&c.Health>95&&c.Interaction.FootIntegrity>95)
            lines=["轻轻看看就好哦。","油正在好好慢慢来呀。"];
        else if(situation=="accessory-high"&&!WorldSimulation.CanSelfMove(c))
            lines=[$"{accessory}在那里……油过不去……","帮油拿回来，好不好？"];
        else if(situation is "released" or "unwrap"&&!WorldSimulation.CanSelfMove(c))
            lines=["已经松开了……脚脚还是痛痛的。","还不能走……让油歇一歇。"];
        else if(situation=="dry"&&c.Interaction.Wetness>5)
            lines=["这里还有湿湿，慢慢擦。","毛巾先生，轻一点哦。"];
        else
        {
            string[]? voice=c.IsYoung?YoungVoice(situation,name,accessory):c.TrueDisposition switch
            {
                "proud"=>ProudVoice(situation,name),
                "shy"=>ShyVoice(situation),
                _=>null
            };
            if(voice!=null)lines=voice;
        }
        int index = c.Interaction.DialogueSequence++ % lines.Length;
        return lines[index];
    }

    private static string[]? YoungVoice(string situation,string name,string accessory)=>situation switch
    {
        "greet"=>[$"{name}在这～！","来玩玩～慢慢来！"],
        "stroke"=>["摸摸～嘿嘿～","还要这里～"],
        "nuzzle"=>["蹭蹭！软软～","再蹭一下下！"],
        "learn"=>["这、这样？","再做一下嘛～"],
        "learn-failed"=>["不对嘛……？","再教教小油嘛……"],
        "learn-success"=>["会啦！看这里！","做到了～夸夸～"],
        "accessory-taken"=>[$"{accessory}！还、还来！","那是小油的！呜……"],
        "sweet"=>["甜甜！幸福～！","小口小口～好吃吃！"],
        "too-tired"=>["累累……","要软软窝……"],
        "comfort"=>["再陪一下下……","手手先别走……"],
        _=>null
    };
    private static string[]? ProudVoice(string situation,string name)=>situation switch
    {
        "greet"=>[$"来得正好，看看{name}今天会什么！","今天也要痛痛快快地慢慢来！"],
        "stroke"=>["嗯，就摸这里。慢慢一点哦。","这只手还挺会摸摸的嘛。"],
        "nuzzle"=>["只有一下……再一下也可以。","油也会蹭蹭！别突然拿开哦。"],
        "learn"=>["先示范一次，油会认真看的。","这个手势是这样吗？让油再试试。"],
        "learn-failed"=>["刚才没做好……再来一次！","油还没弄明白，再慢慢讲一遍。"],
        "learn-success"=>["看到了吧！油真的做到了！","这回做对了，油还想再试试。"],
        "comfort"=>["油、油只是想静一会儿。你先陪着。","刚才真不能慢慢来……再待一会儿吧。"],
        "treasure"=>["这可是油挑中的宝物先生，给你看看！","漂亮吧？看看就好，要还给油哦。"],
        _=>null
    };
    private static string[]? ShyVoice(string situation)=>situation switch
    {
        "greet"=>["是来找油的吗？那……一起慢慢来。","油在这里。慢慢过来就好。"],
        "stroke"=>["轻轻的……这样就好。","手手慢一点，油也慢慢放松。"],
        "nuzzle"=>["靠近一点……嗯，再蹭一下。","油也可以慢慢蹭回来吧……"],
        "learn"=>["油先看看，再慢慢试。","这里要停一下，是吗？"],
        "learn-success"=>["这次……有做好吗？","做到啦。你一直有看着呢。"],
        "treasure"=>["这个亮亮的，想给你看一下。","宝物先生先借你看看……要还回来哦。"],
        _=>null
    };
}
