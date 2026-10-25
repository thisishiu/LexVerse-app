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
      { id: "work-break", source: "Take a short walk between tasks. When you return, read the last paragraph again before writing the next one.", translation: "Đi dạo một lát giữa các công việc. Khi quay lại, hãy đọc lại đoạn cuối trước khi viết đoạn tiếp theo." }
    ] },
    { label: "The art of noticing", blocks: [
      { id: "notice-title", source: "The art of noticing", translation: "Nghệ thuật quan sát" },
      { id: "notice-intro", source: "A good idea often begins with a small observation. Look at the familiar as if you were seeing it for the first time.", translation: "Ý tưởng hay thường bắt đầu từ một quan sát nhỏ. Hãy nhìn điều quen thuộc như thể bạn mới thấy nó lần đầu." },
      { id: "notice-note", source: "Write down one thing that surprised you today. It does not have to be useful yet.", translation: "Ghi lại một điều khiến bạn bất ngờ hôm nay. Chưa cần biết nó có hữu ích hay không." },
      { id: "notice-next", source: "Tomorrow, follow that question a little further. Curiosity grows when we give it time.", translation: "Ngày mai, hãy tìm hiểu câu hỏi ấy sâu hơn một chút. Trí tò mò lớn lên khi ta dành thời gian cho nó." }
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
