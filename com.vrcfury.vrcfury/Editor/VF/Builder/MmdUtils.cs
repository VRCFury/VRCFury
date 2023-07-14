using System.Collections.Immutable;
using System.Linq;

namespace VF.Builder {
    public class MmdUtils {

        private static string text = @"
a
ah
alaugh
anger
angry
beat
berotehe
blink
blinkhappy
calm
calmω
catsnew
ch
cheekblush
cheerful
close><
cormorant
down
e
eyefunky
eyeheart
eyehioff
eyenowidetears
eyerefoff
eyesmall
eyesmallv
eyestar
eyeunderli
get
girls
grin
ha!!!
hachueye
hachume
heart
hellopaste
highlight消
hmm
horrorchild!
hostility
howawa
huh
i
jitoeye
joy
kiri
kirieye
lick
licking
liftingamouth
lightlower
lower
mouse
mouse1
mousedw
mouseup
mousewd
mouthcornerlowering
mouthhornlower
mouthhornraise
mouthsidewiden
mouthspread
n
nagomi
nagomiω
niyari
notoothabove
o
oh
omega
originaljapanese
pero
pupil
pupilverticalcollapse
pupil全消
sad
sadness
serious
slant
smile
smily
stare
stareye
surprised
tearseye
teethnonelower
teethnoneupper
tehhehlick
there
todoit
toothandeyes
toothanon
toothbnon
trouble
u
up
upper
wa
wail
wao?!
what?
wink
winka
winkb
winkc
winkright
withoutteeth
wow?!
your
ω
ω□
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
テレ
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
喜facialexpression
喜び
困る
怒り
恐ろしい子！
悲しむ
悲む
敵意
星目
映り込body消
映り込み消
映り込み消し
歯無し上
歯無し下
涙
涙目
照れ
目の幅涙
眉頭右
眉頭左
真面目
瞳全消し
瞳小
瞳縦潰れ
笑い
輪郭
通常
青ざめる
頬染め
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
            text = text.Replace("_", "");
            text = text.Replace("-", "");
            text = text.Replace(" ", "");
            return text;
        }
        
        public static bool IsMaybeMmdBlendshape(string name) {
            return mmdShapes.Contains(Normalize(name));
        }
    }
}
