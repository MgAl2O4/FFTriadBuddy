# FFTriadBuddy
Helper program for Triple Triad minigame in [Final Fantasy XIV](https://www.finalfantasyxiv.com/), NPC matches only.  
(All used icons are property of SQUARE-ENIX Ltd. All rights reserved)

Features:

* suggest next move in standalone simulation
* suggest next move with in-game overlay (screenshot based)
* organize cards and npc fights
* prepare deck for given npc
* all game rules are supported
* **super secret** Mini Cactpot mode for in-game overlay

## Instructions
1. Download [latest release](https://github.com/MgAl2O4/FFTriadBuddy/releases/latest)
2. Unpack and run. 

Program will attempt to auto update on startup from this repository.

## Bug reports

Bug happens. Whenever it's related to Play: Screenshot mode not recognizing images/cards correctly, please include a screenshot in bug report. Ideally, the one saved by tool in secret-debug-mode, which is exact input image for processing.
* make sure that game show broken state and board is not obscured
* switch to Play: Simulate tab, press [F12] and hit "Apply rule" button
* look for screenshot-source-xx.jpg file, saved next to tool's executable

## Translation

You can now help with translating tool's UI to your own language. Game assets (card names, etc) are limited to game client languages.
* copy sources\loc\strings.resx
* add culture code to file name (ISO 639-1 from https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes)
* translate content of <value> tags
* send file via pull request or email

Contact: MgAl2O4@protonmail.com
