using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Draftline.Unity
{
    public enum GameLanguage { Chinese,English }
    /// <summary>Presentation-only bilingual catalogue. Match commands and snapshots stay language independent.</summary>
    public static class GameLocalization
    {
        public const string PreferenceKey="Driftline.Language";
        sealed class Template { public Regex Pattern;public string English;public int Count; }
        static readonly Dictionary<string,string> words=new Dictionary<string,string>(),reverse=new Dictionary<string,string>(),cache=new Dictionary<string,string>();
        static KeyValuePair<string,string>[] fragments;
        static Template[] templates;
        static bool initialized;
        static GameLanguage language;
        public static event Action Changed;
        public static GameLanguage Language {get{Initialize();return language;}}
        public static void Initialize()
        {
            if(initialized)return;initialized=true;language=(GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey,0),0,1);
            var asset=Resources.Load<TextAsset>("Localization/ChineseEnglish");
            if(asset==null){fragments=Array.Empty<KeyValuePair<string,string>>();templates=Array.Empty<Template>();return;}
            foreach(var raw in asset.text.Split('\n'))
            {
                var line=raw.TrimEnd('\r');if(line.Length==0||line.StartsWith("#"))continue;
                int split=line.IndexOf('|');if(split<=0)continue;
                var source=line.Substring(0,split).Replace("\\n","\n");var english=line.Substring(split+1).Replace("\\n","\n");
                words[source]=english;if(!reverse.ContainsKey(english))reverse[english]=source;
            }
            var list=new List<Template>();
            foreach(var pair in words.Where(p=>Regex.IsMatch(p.Key,@"\{\d+\}")))
            {
                var pattern=new StringBuilder(@"\A");int position=0,count=0;
                foreach(Match match in Regex.Matches(pair.Key,@"\{(\d+)\}"))
                {int index=int.Parse(match.Groups[1].Value);pattern.Append(Regex.Escape(pair.Key.Substring(position,match.Index-position)));pattern.Append("(?<p"+index+">.*?)");position=match.Index+match.Length;count=Math.Max(count,index+1);}
                pattern.Append(Regex.Escape(pair.Key.Substring(position))).Append(@"\z");
                list.Add(new Template{Pattern=new Regex(pattern.ToString(),RegexOptions.Singleline|RegexOptions.CultureInvariant),English=pair.Value,Count=count});
            }
            templates=list.OrderByDescending(t=>t.Pattern.ToString().Length).ToArray();
            fragments=words.Where(p=>!Regex.IsMatch(p.Key,@"\{\d+\}")).OrderByDescending(p=>p.Key.Length).ToArray();
        }
        public static void SetLanguage(GameLanguage value,bool persist=true)
        {
            Initialize();language=value;if(persist){PlayerPrefs.SetInt(PreferenceKey,(int)value);PlayerPrefs.Save();}Changed?.Invoke();
        }
        public static string Original(string text){Initialize();return text!=null&&reverse.TryGetValue(text,out var source)?source:text;}
        public static bool ContainsChinese(string text)=>text!=null&&text.Any(c=>c>=0x4e00&&c<=0x9fff);
        public static string Translate(string text)
        {
            Initialize();if(string.IsNullOrEmpty(text))return text;
            var source=Original(text);if(language==GameLanguage.Chinese)return source;
            if(cache.TryGetValue(source,out var result))return result;
            result=English(source,0);cache[source]=result;
            if(result!=source)reverse[result]=source;
            return result;
        }
        static string English(string source,int depth)
        {
            if(words.TryGetValue(source,out var exact))return exact;
            if(!ContainsChinese(source)||depth>5)return source;
            foreach(var template in templates)
            {
                var match=template.Pattern.Match(source);if(!match.Success)continue;
                var args=new object[template.Count];for(int i=0;i<args.Length;i++)args[i]=English(match.Groups["p"+i].Value,depth+1);
                return string.Format(template.English,args);
            }
            if(source.Contains("\n"))return string.Join("\n",source.Split('\n').Select(line=>English(line,depth+1)));
            foreach(var pair in fragments)source=source.Replace(pair.Key,pair.Value);
            return source.Replace("：",": ").Replace("，",", ").Replace("。",".").Replace("；","; ");
        }
    }
}
