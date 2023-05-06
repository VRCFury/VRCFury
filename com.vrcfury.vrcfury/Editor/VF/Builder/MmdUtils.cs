using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace VF.Builder {
    public class MmdUtils {

        private static string text = @"
bero-tehe
blink
down
eyefunky
eyeheart
eyehi-off
eyeref-off
eyesmall
eyesmall-v
eyestar
eyeunderli
get
angry
ha!!!
hostility
howawa
ω
jito-eye
joy
kiri-eye
mousedw
mouseup
mousewd
mouse_1
mouse_
niyari
o
omega
pero
serious
smile
smily
toothanon
toothbnon
trouble
up
wa
wail
wao?!
wink
wink-a
wink-b
wink-c
a
e
i
n
o
u
ω
∧
□
▲
あ
い
う
え
お
がーん
じと目
てへぺろ
なごみ
なごみω
にこり
にっこり
にやり
はぁと
はぅ
はちゅ目
はちゅ目横潰れ
はちゅ目縦潰れ
びっくり
ぺろっ
まばたき
みっぱい
わぉ?!
ん
ウィンク
ウィンク右
ウィンク
ウィンク右
ハイライト消
ハイライト消し
ハンサム
メガネ
ワ
上
下
光下
前
口横広げ
口角上げ
口角下げ
喜び
困る
怒り
恐ろしい子！
悲しむ
敵意
星目
映り込み消
映り込み消し
歯無し上
歯無し下
涙
照れ
眉頭右
眉頭左
真面目
瞳小
瞳縦潰れ
笑い
輪郭
通常
青ざめる
髪影消
ｳｨﾝｸ右
ｷﾘｯ
";
            
        private static readonly ImmutableHashSet<string> mmdShapes;

        static MmdUtils() {
            mmdShapes = text.Split('\n')
                .Select(line => line.Trim())
                .Select(line => Normalize(line))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToImmutableHashSet();
        }

        private static string Normalize(string text) {
            text = text.ToLower();
            text = text.Replace("2", "");
            text = text.Replace("２", "");
            return text;
        }
        
        public static bool IsMaybeMmdBlendshape(string name) {
            return mmdShapes.Contains(Normalize(name));
        }
    }
}
