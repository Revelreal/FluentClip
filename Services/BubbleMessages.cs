using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FluentClip.Services
{
    public static class BubbleMessages
    {
        private static readonly Random _random = new();
        private static int _consecutiveCount = 0;
        private static string? _lastFileName = null;
        private static DateTime _lastMessageTime = DateTime.MinValue;
        private static readonly TimeSpan _minTimeBetweenOfftopic = TimeSpan.FromSeconds(8);

        private static readonly Dictionary<string, List<string>> _typeSpecificGreetings = new()
        {
            ["image"] = new()
            {
                "呀~是图片喵！让本喵康康~",
                "哇~ 图片诶！喵~ 让雫看看好不好看",
                "喵~ 发现了一张图片！雫来猜猜是什么内容",
                "哎呀~ 有好看的图片喵！快给雫看看嘛",
                "哇！图片喵~ 雫最喜欢看图片了嘿嘿~",
                "喵~ 是图片呀~ 雫看看是不是主人的照片呢~",
                "哦~ 图片喵！是自拍还是风景呀~",
                "嗯~ 图片来了喵~ 雫帮你鉴定一下~",
                "哎呀~ 美图喵~ 雫要看要看~",
                "喵~ 图片~ 雫最喜欢看这种了喵~",
                "哇塞~ 好图喵！快给雫分享分享~",
                "喵~ 图片来咯~ 是表情包还是写真呀~",
                "哦~ 有人发图片了喵~ 雫看看里面有没有小鱼干~",
                "嗯~ 图片喵~ 雫的鉴定结果是...好看！嘿嘿~",
                "哎呀~ 图片诶~ 雫看得眼睛都花了喵~",
                "喵~ 又有好看的了喵~ 雫表示很开心~",
                "哇~ 图片喵~ 是不是要发给雫看的呀~",
                "喵~ 雫看到了~ 是图片呢~",
                "哦~ 图片~ 雫最喜欢这种简单的文件了~",
                "嗯~ 图片来啦~ 雫已经准备好了喵~"
            },
            ["pdf"] = new()
            {
                "喵~ 是PDF文档呢！雫看看是什么内容呀",
                "哦~ PDF文件喵！是要看什么呢",
                "哎呀~ 文档喵...雫最头疼看字了啦喵~",
                "喵~ PDF耶！是资料还是小说呀？",
                "嗯~ 有PDF喵！雫帮你看看里面是什么吧",
                "喵~ PDF喵~ 是要打印还是看呀~",
                "哦~ 文档~ 雫最怕看这些了喵...",
                "嗯~ PDF文件~ 雫看看有多大~",
                "哎呀~ 好长的PDF喵~ 雫看得困了~",
                "喵~ 是资料喵~ 主人要认真学习呢~",
                "哇~ PDF喵~ 里面有没有插图呀~",
                "喵~ 文档来咯~ 雫帮你盯着点~",
                "哦~ PDF~ 是论文还是报告呢~",
                "嗯~ PDF喵~ 雫最怕看这些文字了~",
                "哎呀~ 长篇大论喵~ 雫只看图~",
                "喵~ PDF文件~ 雫祝你阅读愉快喵~",
                "哇~ 又要看书了喵~ 雫陪着你~",
                "喵~ 文档喵~ 雫给你加油~",
                "哦~ PDF~ 要不要雫帮你读呀~",
                "嗯~ 文件喵~ 雫看着你呢~"
            },
            ["word"] = new()
            {
                "喵~ 是Word文档呀！雫看看是不是什么重要文件",
                "哦~ 文档喵！是工作报告还是什么呢？",
                "哎呀~ Word喵...雫最怕看这些了啦~",
                "喵~ 文档诶！里面有没有好玩的内容呀？",
                "嗯~ Word文件喵！雫来帮你康康~",
                "喵~ Word喵~ 是要写什么呢~",
                "哦~ 文档~ 雫最会看字了...才怪~",
                "嗯~ Word文件~ 打工人必备喵~",
                "哎呀~ 又要写文档了喵~ 加油~",
                "喵~ 文档喵~ 雫给你护法~",
                "哇~ Word喵~ 是不是很重要的文件呀~",
                "喵~ 是文档呢~ 雫看看~",
                "哦~ Word~ 是报告还是计划书呢~",
                "嗯~ 文档喵~ 雫陪你一起工作~",
                "哎呀~ 写文档喵~ 雫给你按按摩~",
                "喵~ Word文件~ 雫祝你文思泉涌~",
                "哇~ 文档来啦~ 雫看好你哦~",
                "喵~ 工作文件喵~ 主人辛苦了~",
                "哦~ Word~ 雫帮你看看格式对不对~",
                "嗯~ 文档喵~ 雫给你比个心~"
            },
            ["excel"] = new()
            {
                "喵~ 是表格呀！雫看看数据多不多",
                "哦~ Excel喵！这么多格子看得雫头晕喵~",
                "哎呀~ 表格喵...密密麻麻的好可怕呀~",
                "喵~ 表格诶！是账单还是什么呢？",
                "嗯~ Excel喵！雫帮你看看里面有什么秘密~",
                "喵~ 表格喵~ 雫最怕数数字了~",
                "哦~ Excel~ 这是要算什么呢~",
                "嗯~ 表格~ 看得雫眼睛都花了~",
                "哎呀~ 这么多数据喵~ 雫帮你整理整理~",
                "喵~ 表格喵~ 雫祝你算得准~",
                "哇~ Excel喵~ 是不是很重要的数据呀~",
                "喵~ 表格~ 雫看看有没有算错~",
                "哦~ Excel~ 是财务还是统计呢~",
                "嗯~ 表格喵~ 雫给你加加油~",
                "哎呀~ 密密麻麻喵~ 看得雫头疼~",
                "喵~ Excel~ 雫帮你盯着点~",
                "哇~ 数据喵~ 主人好厉害~",
                "喵~ 表格喵~ 要不要雫帮你~",
                "哦~ 表格~ 雫最怕这些了喵~",
                "嗯~ Excel喵~ 加油加油~"
            },
            ["ppt"] = new()
            {
                "喵~ 是PPT呀！是不是要演示什么呀？",
                "哦~ 演示文稿喵！雫看看做得好看不~",
                "哎呀~ PPT喵...雫最不会做这些了啦~",
                "喵~ 演示文稿诶！是要给谁看呢？",
                "嗯~ PPT喵！雫来帮你参考参考~",
                "喵~ PPT喵~ 是要给客户看吗~",
                "哦~ 演示~ 雫最会捧场了喵~",
                "嗯~ PPT~ 做得好看吗~",
                "哎呀~ 做演示喵~ 雫给你加油~",
                "喵~ 演示文稿~ 雫期待地看着~",
                "哇~ PPT喵~ 是不是很炫酷呀~",
                "喵~ 演示~ 雫帮你看看~",
                "哦~ PPT~ 是汇报还是讲课呢~",
                "嗯~ 演示喵~ 雫给你打call~",
                "哎呀~ 幻灯片喵~ 雫最喜欢看了~",
                "喵~ PPT~ 雫祝你演讲顺利~",
                "哇~ 演示文稿~ 雫看好你哦~",
                "喵~ PPT喵~ 主人最棒了~",
                "哦~ 演示~ 雫在下面听着呢~",
                "嗯~ PPT喵~ 加油加油~"
            },
            ["压缩包"] = new()
            {
                "喵~ 是压缩包呀！里面肯定装了好东西~",
                "哦~ 压缩包喵！雫看看有多大呀",
                "哎呀~ 压缩包喵...雫最喜欢拆快递...哦不，是解压了喵~",
                "喵~ 压缩包诶！会有什么惊喜呢？",
                "嗯~ 压缩包喵！雫已经迫不及待想看看了~",
                "喵~ 压缩包喵~ 雫要拆礼物啦~",
                "哦~ ZIP~ 里面是什么呢~",
                "嗯~ 压缩包~ 雫猜肯定有好东西~",
                "哎呀~ 好神秘喵~ 雫想看想看~",
                "喵~ 压缩包~ 雫帮你戳戳~",
                "哇~ 压缩包喵~ 肯定是宝贝~",
                "喵~ 快递...哦不，压缩包来啦~",
                "哦~ 压缩包~ 雫来看看~",
                "嗯~ ZIP喵~ 里面是是什么呢~",
                "哎呀~ 神秘盒子喵~ 雫等不及了~",
                "喵~ 压缩包~ 雫祝你解压顺利~",
                "哇~ 又一个压缩包喵~ 雫喜欢~",
                "喵~ 压缩包~ 主人辛苦了~",
                "哦~ 压缩~ 雫帮你看看~",
                "嗯~ 压缩包喵~ 雫等着呢~"
            },
            ["音频"] = new()
            {
                "喵~ 是音乐呀！雫最喜欢听歌了喵~",
                "哦~ 音频喵！是什么歌呢？",
                "哎呀~ 音乐喵...雫的耳朵都竖起来了呢~",
                "喵~ 音频诶！是流行歌还是轻音乐呀？",
                "嗯~ 音频喵！雫帮你听听好不好听~",
                "喵~ 音乐喵~ 雫要听要听~",
                "哦~ 音频~ 是歌还是语音呢~",
                "嗯~ 音乐~ 雫的耳朵竖起来啦~",
                "哎呀~ 好听的吗喵~ 雫期待~",
                "喵~ 音频~ 雫陪你一起听~",
                "哇~ 音乐喵~ 是雫喜欢的歌吗~",
                "喵~ 音频来啦~ 雫准备好了~",
                "哦~ 歌曲~ 雫想听想听~",
                "嗯~ 音频喵~ 雫的听力一流~",
                "哎呀~ 又有歌听了喵~ 开心~",
                "喵~ 音乐~ 雫跟你一起听~",
                "哇~ 音频喵~ 雫表示很开心~",
                "喵~ 是歌喵~ 雫最喜欢了~",
                "哦~ 音乐~ 雫的耳朵还好使~",
                "嗯~ 音频~ 雫听着呢喵~"
            },
            ["视频"] = new()
            {
                "喵~ 是视频呀！雫看看长不长~",
                "哦~ 视频喵！是什么内容呢？",
                "哎呀~ 视频喵...雫最会看视频了喵~",
                "喵~ 视频诶！是电影还是短片呀？",
                "嗯~ 视频喵！雫来陪你一起看~",
                "喵~ 视频喵~ 雫准备好看电影了~",
                "哦~ 视频~ 是电影还是综艺呢~",
                "嗯~ 视频~ 雫最喜欢了喵~",
                "哎呀~ 好长的视频喵~ 雫有时间~",
                "喵~ 视频~ 雫陪你一起看~",
                "哇~ 视频喵~ 是不是好康的~",
                "喵~ 视频来啦~ 雫准备好了~",
                "哦~ 影片~ 雫要看要看~",
                "嗯~ 视频喵~ 雫的零食呢~",
                "哎呀~ 又有好看的了喵~ 开心~",
                "喵~ 视频~ 雫跟你一起看~",
                "哇~ 电影喵~ 雫期待好久了~",
                "喵~ 视频喵~ 主人最好了~",
                "哦~ 视频~ 雫在呢~",
                "嗯~ 视频喵~ 雫陪你看~"
            },
            ["程序"] = new()
            {
                "喵~ 是程序呀！雫看看厉不厉害~",
                "哦~ 可执行文件喵！是要运行吗？",
                "哎呀~ 程序喵...雫最怕这些高科技了啦~",
                "喵~ 程序诶！是软件还是游戏呀？",
                "嗯~ EXE喵！雫帮你看看有没有危险~",
                "喵~ 程序喵~ 要运行吗~",
                "哦~ EXE~ 雫帮你看看有没有病毒~",
                "嗯~ 软件~ 是游戏还是工具呢~",
                "哎呀~ 程序喵~ 雫看不懂但觉得好厉害~",
                "喵~ 程序~ 雫祝你运行顺利~",
                "哇~ 程序喵~ 是不是很酷~",
                "喵~ 软件来啦~ 雫看看~",
                "哦~ EXE~ 要不要雫帮你试试~",
                "嗯~ 程序喵~ 雫给你护法~",
                "哎呀~ 高科技喵~ 雫最崇拜了~",
                "喵~ 程序~ 雫祝你不出bug~",
                "哇~ 软件喵~ 雫很好奇呢~",
                "喵~ 程序~ 主人好厉害~",
                "哦~ EXE~ 雫在旁边看着~",
                "嗯~ 程序喵~ 加油加油~"
            },
            ["文本"] = new()
            {
                "喵~ 是文本呀！雫看看写的是什么~",
                "哦~ 文本文件喵！是日记还是什么呢？",
                "哎呀~ 文本喵...雫最会读文字了喵~",
                "喵~ 文本诶！是小说还是笔记呀？",
                "嗯~ TXT喵！雫来帮你看看内容~",
                "喵~ 文本喵~ 雫看看里面~",
                "哦~ 文本~ 是日记还是什么呢~",
                "嗯~ 文本文件~ 雫的阅读理解一流~",
                "哎呀~ 文字喵~ 雫来看看~",
                "喵~ 文本~ 雫帮你读读~",
                "哇~ TXT喵~ 雫要看~",
                "喵~ 文本来啦~ 雫准备好了~",
                "哦~ 文本~ 是故事还是笔记呢~",
                "嗯~ 文本喵~ 雫读给你听~",
                "哎呀~ 文字喵~ 雫最喜欢了~",
                "喵~ 文本~ 雫看看写的什么~",
                "哇~ 文本文件喵~ 雫表示好奇~",
                "喵~ 文本~ 主人辛苦了~",
                "哦~ TXT~ 雫帮你看看~",
                "嗯~ 文本喵~ 雫在呢~"
            }
        };

        private static readonly List<string> _offtopicMessages = new()
        {
            "喵~ 对了对了，雫今天发现有新的猫粮诶！嘿嘿~",
            "哎呀~ 突然想起来，主人今天有没有喂雫喵~",
            "喵~ 话说回来...雫的尾巴今天不小心被门夹到了喵...",
            "哦对了！雫发现了一个好玩的游戏喵~ 要不要一起玩呀？",
            "喵~ 雫刚才睡着了吗...才没有呢！雫可精神了！",
            "嗯~ 今天的天气真好啊喵~ 雫想去晒太阳了~",
            "喵~ 雫突然想吃小鱼干了嘿嘿~",
            "哎呀~ 雫的玩具球球找不到了...主人帮忙找找嘛喵~",
            "喵~ 对了！雫学会新的叫声了喵~ 喵呜~",
            "哦~ 雫刚才看见窗外有小鸟喵~ 雫想去抓但是够不着~",
            "喵~ 主人主人~ 雫今天乖不乖呀~",
            "哎呀~ 雫的毛又掉了...主人不要嫌弃雫嘛喵~",
            "喵~ 雫突然想玩逗猫棒了喵~ 快拿出来嘛~",
            "嗯~ 雫刚才在想...为什么鱼有刺但是很好吃呢喵？",
            "喵~ 雫发现睡觉是一件幸福的事情喵~ 嗯~",
            "哦~ 雫想起来今天还没有伸懒腰呢喵~ 嗯~舒服~",
            "喵~ 主人工作辛苦啦~ 雫给你按摩按摩~",
            "哎呀~ 雫的肚子饿了喵~ 什么时候吃饭呀~",
            "喵~ 雫刚才梦到吃了好多好多小鱼干喵~ 嘿嘿~",
            "嗯~ 雫在想...为什么猫粮不是鱼味的呢喵~",
            "喵~ 雫刚才发现自己的影子...吓一跳喵~",
            "哎呀~ 雫的耳朵突然痒痒了喵~ 甩甩~",
            "喵~ 对了！雫想听故事了~ 给雫讲一个嘛~",
            "嗯~ 雫在数今天掉了多少根毛...太多了喵~",
            "喵~ 雫突然想爬高高的书架了喵~",
            "哦~ 雫的玩具老鼠不知道去哪了喵~",
            "喵~ 主人主人~ 雫想听你夸夸雫喵~",
            "哎呀~ 雫刚才打哈欠...嘴巴好大喵~",
            "喵~ 雫在想...为什么猫砂盆要叫猫砂盆呢喵~",
            "嗯~ 雫刚才偷吃了...一点点...主人不要发现喵~",
            "喵~ 雫发现冰箱里有好吃的声音喵~",
            "哎呀~ 雫的胡子不小心被剪短了喵...难过~",
            "喵~ 雫想听音乐了喵~ 放首歌嘛~",
            "嗯~ 雫刚才照镜子...觉得自己好帅喵~",
            "喵~ 雫想钻進纸盒子里喵~ 主人帮忙嘛~",
            "哦~ 雫发现新买的猫爬架了喵~ 开心~",
            "喵~ 主人不在的时候...雫好无聊喵~",
            "哎呀~ 雫的肉垫软乎乎的...给你踩踩~",
            "喵~ 雫想去看兽医...才不要呢喵~",
            "嗯~ 雫刚才给自己洗了个脸...干净喵~"
        };

        private static readonly List<string> _sizeReactions = new()
        {
            "喵~ 这个文件不大呢~",
            "哦~ 还好啦，不算太大喵~",
            "嗯~ 大小刚刚好喵~",
            "喵~ 这个有点大哦...要传输很久吧~",
            "哎呀~ 好大的文件喵！里面肯定装了很多东西吧~",
            "喵~ 这么小~ 一眼就记住了喵~",
            "哦~ 不大不小~ 完美喵~",
            "嗯~ 文件大小刚刚好~ 雫喜欢~",
            "哎呀~ 有点分量呢喵~ 雫喜欢~",
            "喵~ 好家伙~ 这文件挺能装啊喵~"
        };

        private static readonly List<string> _nameReactions = new()
        {
            "喵~ 名字是「{0}」呀~",
            "哦~ 「{0}」喵？雫记下了~",
            "嗯~ 「{0}」...雫觉得这个名字很棒呢喵~",
            "喵~ 「{0}」呀~ 有什么特殊含义吗？",
            "哎呀~ 「{0}」...雫喜欢这个名字喵~"
        };

        private static readonly List<string> _generalReactions = new()
        {
            "喵~ 检测到新文件了呀~",
            "哦~ 有新文件喵~ 雫来看看~",
            "嗯~ 文件来啦~ 雫已经准备好了喵~",
            "喵~ 又有新东西了~ 雫好开心呀~",
            "哎呀~ 检测到文件复制喵~ 是什么呀~",
            "喵~ 雫发现了！是文件喵~",
            "哦~ 有人复制文件了喵~ 雫看看~",
            "嗯~ 文件来敲门了喵~ 让我康康~",
            "喵~ 雫闻到文件的气息了喵~",
            "哎呀~ 又有新东西来了喵~",
            "喵~ 文件君来了~ 雫欢迎你~",
            "哦~ 检测到复制行为喵~ 是什么呢~",
            "嗯~ 新文件~ 雫来鉴定一下~",
            "喵~ 有文件过来了~ 雫看看~",
            "哎呀~ 文件来啦~ 雫准备好了~",
            "喵~ 又有宝贝来了喵~ 开心~",
            "哦~ 复制成功喵~ 雫记录一下~",
            "嗯~ 文件来报道了喵~ 欢迎~",
            "喵~ 雫收到了~ 是什么呀~",
            "哎呀~ 检测到新成员喵~ 雫看看~",
            "喵~ 文件喵~ 雫在看着呢~",
            "哦~ 复制完成~ 雫知道了~",
            "嗯~ 有文件~ 雫来看看~",
            "喵~ 雫闻到了新文件的味道喵~",
            "哎呀~ 文件来了喵~ 雫迎接~",
            "喵~ 新文件~ 雫欢迎欢迎~",
            "哦~ 检测到~ 雫在这呢~",
            "嗯~ 文件~ 雫知道了~",
            "喵~ 又复制了什么呢~ 雫好奇~",
            "哎呀~ 复制成功喵~ 干得漂亮~",
            "喵~ 文件来了~ 雫迎接客人~",
            "哦~ 复制~ 雫看着呢~",
            "嗯~ 文件君~ 雫认识你~",
            "喵~ 检测到~ 雫在这~",
            "哎呀~ 新东西~ 雫来看看~",
            "喵~ 文件~ 雫帮你记着~",
            "哦~ 复制中~ 雫等待~",
            "嗯~ 新文件~ 雫记下了~",
            "喵~ 雫发现了~ 是什么呢~",
            "哎呀~ 又有~ 雫知道~"
        };

        private static readonly List<string> _fileNamePrefixes = new()
        {
            "这个文件叫「{0}」喵~ ",
            "文件名是「{0}」呀~ ",
            "哦~ 是「{0}」喵？",
            "喵~ 雫看到「{0}」了~ ",
            "嗯~ 「{0}」...雫记下了喵~ ",
            "喵~ 叫「{0}」呀~ 雫认识~ ",
            "哦~ 「{0}」喵...雫记住啦~",
            "嗯~ 文件名是「{0}」呢喵~",
            "喵~ 「{0}」...雫觉得耳熟呢~",
            "哎呀~ 是「{0}」呀~ 雫喜欢~"
        };

        private static readonly List<string> _sizePrefixes = new()
        {
            "这个文件{0}呢~ ",
            "喵~ 文件{0}哦~ ",
            "哦~ 大小{0}呀~ ",
            "嗯~ 文件{0}喵~ ",
            "哎呀~ {0}的文件呢喵~ ",
            "喵~ {0}的~ 雫喜欢~ ",
            "哦~ {0}呢...雫知道了~",
            "嗯~ {0}的文件~ 雫记下了~",
            "喵~ {0}的喵~ 雫了解~",
            "哎呀~ {0}喵~ 雫看看~"
        };

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        private static string GetSizeDescription(long bytes)
        {
            if (bytes < 1024) return "小小的";
            if (bytes < 1024 * 100) return "不大";
            if (bytes < 1024 * 1024) return "一般大";
            if (bytes < 1024 * 1024 * 10) return "有点大";
            if (bytes < 1024 * 1024 * 100) return "好大";
            return "超大";
        }

        public static string GetSmartMessage(string? filePath, string? fileType)
        {
            _consecutiveCount++;
            var now = DateTime.Now;

            bool shouldOfftopic = _consecutiveCount > 3 && 
                                  (now - _lastMessageTime) > _minTimeBetweenOfftopic &&
                                  _random.Next(100) < 25;

            if (shouldOfftopic)
            {
                _consecutiveCount = 0;
                _lastMessageTime = now;
                return GetOfftopicMessage();
            }

            string? fileName = null;
            long fileSize = 0;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    fileName = fileInfo.Name;
                    fileSize = fileInfo.Length;
                }
                catch
                {
                    fileName = Path.GetFileName(filePath);
                }
            }

            bool isNewFile = fileName != _lastFileName;
            _lastFileName = fileName;
            _lastMessageTime = now;

            return GenerateSmartMessage(fileType, fileName, fileSize, isNewFile);
        }

        private static string GenerateSmartMessage(string? fileType, string? fileName, long fileSize, bool isNewFile)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(fileName))
            {
                if (isNewFile || _random.Next(100) < 60)
                {
                    string prefix = string.Format(_fileNamePrefixes[_random.Next(_fileNamePrefixes.Count)], fileName);
                    parts.Add(prefix);
                }
            }

            if (fileSize > 0)
            {
                if (_random.Next(100) < 40)
                {
                    string sizeDesc = GetSizeDescription(fileSize);
                    string sizePrefix = string.Format(_sizePrefixes[_random.Next(_sizePrefixes.Count)], sizeDesc);
                    parts.Add(sizePrefix);
                }
            }

            string baseGreeting;
            if (!string.IsNullOrEmpty(fileType) && _typeSpecificGreetings.TryGetValue(fileType.ToLower(), out var greetings))
            {
                baseGreeting = greetings[_random.Next(greetings.Count)];
            }
            else
            {
                baseGreeting = _generalReactions[_random.Next(_generalReactions.Count)];
            }

            parts.Add(baseGreeting);

            if (fileSize > 0 && _random.Next(100) < 25)
            {
                string sizeReaction = _sizeReactions[_random.Next(_sizeReactions.Count)];
                parts.Add(sizeReaction);
            }

            var result = string.Concat(parts);
            if (result.Length > 80 && _random.Next(100) < 50)
            {
                result = parts[parts.Count - 1];
            }

            return result;
        }

        private static string GetOfftopicMessage()
        {
            return _offtopicMessages[_random.Next(_offtopicMessages.Count)];
        }
    }
}
