# Instruction
- Let's start coding an app here called Lexverse.6
- This app supports translation without having to copy the text and then switch tabs to Google Translate or something similar to translate the word, because sometimes the word might be in an image or similar format, making copying impossible.
- Multilingual translation (default is English to Vietnamese)
- `.env` and `lexverse-42d205709f8e.json` are necessary to call the Google Translate API.
## Features (mode)
### Translate popup
- The feature can be activated as a shortcut or an option on UI.
- When activated, users can select an area on the screen, which can contain a word, a paragraph, or a part of an image with the content to be translated. After creating that area, a popup will appear above or below it and translate that area. 
- Users can interact with each word in that area (For example, when you hover your mouse over a word, that word gets highlighted.), and the popup will highlight the corresponding translated word. The purpose is to help users understand which word is translated into which. 
- Keywords or technical terms can be kept in the popup along with annotations for deeper understanding.
- This feature can be used for reading academic texts, documents, or for multitasking translation (applicable in most cases), etc.
- See an sample at `media\sample-1.png` and `media\sample-2.png` (just sample, real product will be better)
### Real-time translate
- This feature requires real-time translation while maintaining accuracy.
- It can translate across the entire screen, a specific area of ​​the screen, a particular app, etc., overlaying Vietnamese text on top of the text being translated while maintaining a seamless flow. The translation moves smoothly as the content scrolls.
- This feature can be used for purposes such as playing games, reading comics, watching movies with foreign language subtitles, etc.
## Constrains
### UI/UX
- Use folder `media\logo.jpg` to retrieve the logo or `media\logo-lexverse.jpg` for thumbnail (maybe at the app's opening).
- The app may be looks like 'media\demo.png' when it opened.
- Since the product will be launched into the real world, the interface must be beautiful, easy to use, and user-friendly.
- Popups should be easy to understand and user-friendly, and can be enhanced with subtle effects (such as a faint blurring effect to increase user-friendliness).
### Translate
#### Translate popup
- A visually appealing and user-friendly popup is important because this is what users will frequently interact with after opening the app.
#### Real-time translate
- If you are translating for an app, it is unacceptable for the Vietnamese text to overlap with the Vietnamese text in another app after translation.
### Coding
- Implement code that is easy to scale for later use.
- Using git to manage code.
- Creating skill for the project.
- Making a file that tracking project process for subsequent prompts.
- Fronend can use css and js if needed.