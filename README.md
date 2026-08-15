# Intro
This is a Playnite extension that gives you ability to create a custom per-game folder related path to your playnite directory by using built in {PlayniteDir} function so it can be able to use on both normal version and portable version of playnite. It was originally create to serve personal purpose of saving IDM export and media related to a game without doing 200 extra steps to create a folder and forgot where it was 2 days later.

The given setting page that can be access via Add-ons/ Extension settings/ Generic/ CustomFolder should contains:
## Storage Location
Storage location indicate where your CustomFolder going to look like, base preset is ``` {PlayniteDir}\CustomFolder ```
<img width="737" height="164" alt="image" src="https://github.com/user-attachments/assets/dc9bf46e-e0db-46e1-9262-0c9c89ea02de" />

**CAUTION: It would merge into any folder that has the same name so make sure you know what you are doing**, _it won't delete anything it just going to be annoying to navigate in file explorer if you don't know what you just did._

There is also given live preview of what the folder address going to look like under preview tab 

<img width="608" height="125" alt="Screenshot 2026-08-15 001745" src="https://github.com/user-attachments/assets/9cf000cc-ff1f-4c9c-9b69-6e3354ae0dc4" /> (this is not portable ver)
<img width="490" height="105" alt="image" src="https://github.com/user-attachments/assets/2febe759-45d7-4573-a264-5ec9df7e4096" /> (portable ver)
# How to use
1. Change your folder to your desire name (or not)
2. Right click on a game and click on ```Custom Folder``` and it will automatically create and pop a folder based on set-up destination + Game name, it also merge if the name fit and work like normal directory so it technically can go much deep in or even outside playnite folder by using {PlayniteDir}\..\

You will also notice a ``` Theme integration ``` with checkbox and stuff but I hate to tell you that **it is not included**, as I said this was created originally for personal purpose so I directly tweaked ``` Mythic ``` desktop theme and added all value that based on another extension so it can be shown on my detail panel as a quality of life function like underneath, so technically all the value exist but you can't implement on your theme without tweaking theme setting. 

<img width="80" height="60" alt="image" src="https://github.com/user-attachments/assets/da4962dc-2332-4573-9800-1ff9f81bef9e" />

