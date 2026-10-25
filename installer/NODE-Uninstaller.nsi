; NODE Uninstaller v0.1.0 installer. Build from repository root using:
; "C:\Program Files (x86)\NSIS\makensis.exe" installer\NODE-Uninstaller.nsi
; Compile AFTER dotnet publish creates dist\NODE-Uninstaller\NODE.Uninstaller.exe.

Unicode true
!include "MUI2.nsh"

!define PRODUCT_NAME "NODE Uninstaller"
!define PRODUCT_VERSION "0.1.0"
!define PRODUCT_PUBLISHER "NODE Development"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\NODE.Uninstaller"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "dist\NODE-Uninstaller-Setup-v0.1.0.exe"
InstallDir "$PROGRAMFILES64\NODE Development\NODE Uninstaller"
InstallDirRegKey HKLM "${UNINSTALL_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show

!define MUI_ICON "src\NODE.Uninstaller\Assets\NODE-Uninstaller.ico"
!define MUI_UNICON "src\NODE.Uninstaller\Assets\NODE-Uninstaller.ico"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "NODE Uninstaller" MainSection
  SetOutPath "$INSTDIR"
  File /r "dist\NODE-Uninstaller\*.*"
  CreateDirectory "$SMPROGRAMS\NODE Development"
  CreateShortcut "$SMPROGRAMS\NODE Development\NODE Uninstaller.lnk" "$INSTDIR\NODE.Uninstaller.exe"
  CreateShortcut "$DESKTOP\NODE Uninstaller.lnk" "$INSTDIR\NODE.Uninstaller.exe"
  WriteUninstaller "$INSTDIR\Uninstall-NODE-Uninstaller.exe"

  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\NODE.Uninstaller.exe"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall-NODE-Uninstaller.exe$\"'
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\NODE Development\NODE Uninstaller.lnk"
  RMDir "$SMPROGRAMS\NODE Development"
  Delete "$DESKTOP\NODE Uninstaller.lnk"
  ; The app is published as one executable. Do not recursively delete arbitrary
  ; user-created files that might exist in the installation folder.
  Delete "$INSTDIR\NODE.Uninstaller.exe"
  Delete "$INSTDIR\NODE.Uninstaller.pdb"
  Delete "$INSTDIR\Uninstall-NODE-Uninstaller.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${UNINSTALL_KEY}"
SectionEnd
