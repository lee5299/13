; 업무정리 설치 파일(Inno Setup 6). build.ps1 -Package 가 실행 폴더를 만든 뒤 이 스크립트를 컴파일한다.
; 관리자 권한 없이 사용자 폴더에 설치하고, 시작 메뉴에 등록한다. 제거해도 업무자료·백업·복구정보는 지우지 않는다.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\실행"
#endif
#ifndef OutputDir
  #define OutputDir "..\배포"
#endif

[Setup]
AppId={{6B7F2C1A-9D4E-4C3B-8A51-2E7D90F1C4A8}
AppName=업무정리
AppVersion={#AppVersion}
AppVerName=업무정리 {#AppVersion}
AppPublisher=업무정리(개인 과제)
DefaultDirName={localappdata}\Programs\업무정리
DefaultGroupName=업무정리
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=업무정리_설치_{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\업무정리.exe
CloseApplications=yes

[Languages]
#if FileExists(AddBackslash(CompilerPath) + "Languages\Korean.isl")
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
#else
Name: "english"; MessagesFile: "compiler:Default.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
; 기기별 설정·시험자료는 사용자 설정 폴더(%LOCALAPPDATA%\업무정리)에 생기므로 설치 파일에 넣지 않는다.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "프로그램설정.json,시험자료\*,*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\업무정리"; Filename: "{app}\업무정리.exe"
Name: "{autoprograms}\업무정리 (시험자료로 실행)"; Filename: "{app}\업무정리.exe"; Parameters: "--demo"
Name: "{autodesktop}\업무정리"; Filename: "{app}\업무정리.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\업무정리.exe"; Description: "업무정리 실행"; Flags: nowait postinstall skipifsilent
