export type AppId = "article" | "game" | "letters" | "comic";
export interface TextBlock { id: string; source: string; translation: string }
export interface PageContent { label: string; blocks: TextBlock[] }
export interface ContentApp { name: string; sourceLanguage: string; targetLanguage: string; pages: PageContent[] }

export const apps: Record<AppId, ContentApp> = {
  article: { name: "Fieldnotes Reader", sourceLanguage: "en", targetLanguage: "vi", pages: [
    { label: "A quieter way to work", blocks: [
      { id: "work-title", source: "A quieter way to work", translation: "Một cách làm việc tĩnh lặng hơn" },
      { id: "work-intro", source: "Great tools disappear into your workflow. They help you understand more, without asking you to leave the page.", translation: "Công cụ tốt hòa vào nhịp làm việc. Chúng giúp bạn hiểu nhiều hơn mà không cần rời khỏi trang đang đọc." },
      { id: "work-focus", source: "Keep your focus. Let curiosity lead the way.", translation: "Giữ trọn sự tập trung. Để trí tò mò dẫn lối." },
      { id: "work-ritual", source: "Start with one open document and a clear intention. Close the tabs you no longer need. A little space on your screen can make room for a better idea.", translation: "Bắt đầu với một tài liệu đang mở và một mục tiêu rõ ràng. Đóng những tab không còn cần thiết. Một chút khoảng trống trên màn hình có thể nhường chỗ cho ý tưởng tốt hơn." },
      { id: "work-break", source: "Take a short walk between tasks. When you return, read the last paragraph again before writing the next one.", translation: "Đi dạo một lát giữa các công việc. Khi quay lại, hãy đọc lại đoạn cuối trước khi viết đoạn tiếp theo." },
      { id: "work-space", source: "A calmer workspace does not ask you to do less. It gives each thought enough space to become clear before the next one arrives.", translation: "Không gian làm việc yên tĩnh không bắt bạn làm ít đi. Nó cho mỗi ý nghĩ đủ chỗ để trở nên rõ ràng trước khi ý tiếp theo xuất hiện." },
      { id: "work-question", source: "Before opening another tool, ask what you are trying to understand. A useful question can save more time than a faster button.", translation: "Trước khi mở thêm công cụ, hãy hỏi mình đang muốn hiểu điều gì. Một câu hỏi đúng có thể tiết kiệm nhiều thời gian hơn một nút bấm nhanh." },
      { id: "work-connection", source: "Good ideas rarely arrive in a straight line. They grow when notes, conversations and quiet moments have a chance to meet.", translation: "Ý tưởng hay hiếm khi đến theo đường thẳng. Chúng lớn lên khi ghi chú, trò chuyện và những phút lặng yên có dịp gặp nhau." },
      { id: "work-pause", source: "Pause when a sentence matters. Read it once more, then decide whether it deserves a place in what you make next.", translation: "Hãy dừng lại khi một câu văn có ý nghĩa. Đọc thêm lần nữa, rồi quyết định liệu nó có chỗ trong điều bạn sắp tạo ra hay không." },
      { id: "work-close", source: "At the end of the day, leave one clear note for tomorrow. Progress is easier to find when you know where to begin.", translation: "Cuối ngày, hãy để lại một ghi chú rõ ràng cho ngày mai. Bạn sẽ dễ tiếp tục hơn khi biết mình cần bắt đầu từ đâu." }
    ] },
    { label: "The art of noticing", blocks: [
      { id: "notice-title", source: "The art of noticing", translation: "Nghệ thuật quan sát" },
      { id: "notice-intro", source: "A good idea often begins with a small observation. Look at the familiar as if you were seeing it for the first time.", translation: "Ý tưởng hay thường bắt đầu từ một quan sát nhỏ. Hãy nhìn điều quen thuộc như thể bạn mới thấy nó lần đầu." },
      { id: "notice-note", source: "Write down one thing that surprised you today. It does not have to be useful yet.", translation: "Ghi lại một điều khiến bạn bất ngờ hôm nay. Chưa cần biết nó có hữu ích hay không." },
      { id: "notice-next", source: "Tomorrow, follow that question a little further. Curiosity grows when we give it time.", translation: "Ngày mai, hãy tìm hiểu câu hỏi ấy sâu hơn một chút. Trí tò mò lớn lên khi ta dành thời gian cho nó." },
      { id: "notice-look", source: "Notice the route you take every morning. Which detail has always been there, waiting for you to slow down?", translation: "Hãy quan sát con đường bạn đi mỗi sáng. Chi tiết nào vẫn luôn ở đó, chờ bạn chậm lại để nhận ra?" },
      { id: "notice-pattern", source: "Patterns are easier to see when you collect small moments instead of searching for a grand answer all at once.", translation: "Bạn sẽ dễ thấy những quy luật hơn khi gom nhặt các khoảnh khắc nhỏ, thay vì tìm ngay một câu trả lời lớn lao." },
      { id: "notice-walk", source: "Take a walk without a destination. Let a storefront, a conversation or a changing sky interrupt your usual thoughts.", translation: "Hãy đi dạo mà không cần điểm đến. Để một ô cửa hàng, một cuộc trò chuyện hay bầu trời đổi sắc làm gián đoạn lối nghĩ quen thuộc." },
      { id: "notice-share", source: "Tell someone what you found. An ordinary detail can become a new idea when another person sees it from a different angle.", translation: "Kể cho ai đó về điều bạn tìm thấy. Một chi tiết bình thường có thể thành ý tưởng mới khi người khác nhìn nó từ góc độ khác." },
      { id: "notice-return", source: "Come back to your notes next week. What seemed small today may be the beginning of something worth pursuing.", translation: "Tuần sau, hãy đọc lại ghi chú. Điều tưởng chừng nhỏ bé hôm nay có thể là khởi đầu của một việc đáng theo đuổi." }
    ] }
  ] },
  game: { name: "Starfall", sourceLanguage: "ja", targetLanguage: "en", pages: [
    { label: "星の谷への旅", blocks: [
      { id: "quest-one-title", source: "星の谷への旅", translation: "Journey to Starfall Valley" },
      { id: "quest-one-goal", source: "夜明けまでに、古い灯台へ向かおう。", translation: "Reach the old lighthouse before dawn." },
      { id: "quest-one-detail", source: "森の道を進み、灯台守のリラを探してください。", translation: "Follow the forest path and find Lyra, the lighthouse keeper." },
      { id: "quest-one-reward", source: "報酬：星のかけら × 3　EXP +240", translation: "Reward: Star fragments × 3 · EXP +240" }
    ] },
    { label: "灯台の約束", blocks: [
      { id: "quest-two-title", source: "灯台の約束", translation: "The Lighthouse Promise" },
      { id: "quest-two-goal", source: "集めた星のかけらをリラに届けよう。", translation: "Bring the collected star fragments to Lyra." },
      { id: "quest-two-detail", source: "光が戻れば、船は無事に港へ帰れます。", translation: "Once the light returns, the ships can reach the harbor safely." },
      { id: "quest-two-reward", source: "報酬：月の指輪 × 1　EXP +480", translation: "Reward: Moon ring × 1 · EXP +480" }
    ] }
  ] },
  letters: { name: "Postbox", sourceLanguage: "ko", targetLanguage: "vi", pages: [
    { label: "작은 응원의 편지", blocks: [
      { id: "letter-one-title", source: "작은 응원의 편지", translation: "Một lá thư động viên nho nhỏ" },
      { id: "letter-one-slow", source: "천천히 가도 괜찮아요. 중요한 건 멈추지 않는 거예요.", translation: "Đi chậm cũng không sao. Điều quan trọng là đừng dừng lại." },
      { id: "letter-one-start", source: "당신의 새로운 시작을 응원해요.", translation: "Tôi luôn cổ vũ cho khởi đầu mới của bạn." }
    ] },
    { label: "주말에 만나요", blocks: [
      { id: "letter-two-title", source: "주말에 만나요", translation: "Hẹn gặp cuối tuần nhé" },
      { id: "letter-two-meet", source: "토요일 오후에 강변을 함께 걸을까요?", translation: "Chiều thứ Bảy mình cùng đi dạo bên bờ sông nhé?" },
      { id: "letter-two-cafe", source: "산책이 끝나면 작은 카페에서 따뜻한 차를 마셔요.", translation: "Đi dạo xong, mình uống trà nóng ở một quán cà phê nhỏ nhé." }
    ] }
  ] },
  comic: { name: "After Rain Reader", sourceLanguage: "zh", targetLanguage: "vi", pages: [
    { label: "雨后的约定 · 01", blocks: [
      { id: "comic-one-star", source: "你看，第一颗星星！", translation: "Nhìn kìa, ngôi sao đầu tiên!" },
      { id: "comic-one-tomorrow", source: "明天也一起看吧。", translation: "Ngày mai mình lại cùng ngắm nhé." }
    ] },
    { label: "雨后的约定 · 02", blocks: [
      { id: "comic-two-rain", source: "雨终于停了。", translation: "Cuối cùng mưa cũng tạnh rồi." },
      { id: "comic-two-home", source: "走吧，我们一起回家。", translation: "Đi thôi, mình cùng về nhà nào." }
    ] }
  ] }
};

export const gameChrome: Record<string, TextBlock> = {
  header: { id: "game-header-log", source: "旅の記録", translation: "Journey Log" },
  journal: { id: "game-journal", source: "冒険の記録", translation: "Adventure Journal" },
  menuOne: { id: "game-menu-one", source: apps.game.pages[0].label, translation: apps.game.pages[0].blocks[0].translation },
  menuTwo: { id: "game-menu-two", source: apps.game.pages[1].label, translation: apps.game.pages[1].blocks[0].translation },
  objective: { id: "game-objective", source: "目的", translation: "Objective" },
  track: { id: "game-track", source: "このクエストを追跡", translation: "Track this quest" },
  tracking: { id: "game-tracking", source: "✓ 追跡中", translation: "✓ Tracking" },
  next: { id: "game-next", source: "次のクエスト →", translation: "Next quest →" },
  previous: { id: "game-previous", source: "← 前のクエスト", translation: "← Previous quest" }
};

export const terms: Record<string, [string, string][]> = {
  "letter-one-slow": [["천천히", "chậm rãi, từ tốn"], ["멈추다", "dừng lại"]],
  "letter-one-start": [["응원하다", "cổ vũ, động viên"]],
  "letter-two-meet": [["강변", "bờ sông"], ["함께", "cùng nhau"]],
  "letter-two-cafe": [["산책", "đi dạo"], ["따뜻하다", "ấm áp"]]
};
export function blocksFor(app: AppId, page: number) { return apps[app].pages[page].blocks; }
export interface Bounds { x: number; y: number; width: number; height: number }
export function centerInside(block: Bounds, region: Bounds): boolean {
  const x = block.x + block.width / 2, y = block.y + block.height / 2;
  return x >= region.x && x <= region.x + region.width && y >= region.y && y <= region.y + region.height;
}
