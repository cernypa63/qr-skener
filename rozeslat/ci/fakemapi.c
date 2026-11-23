/* Zkušební MAPI poštovní program pro CI: každé MAPISendMail(W) zapíše do souboru FAKEMAPI_OUT. */
#include <windows.h>
#include <stdio.h>
#include <MAPI.h>

static void zapis(const char *s)
{
    char cesta[MAX_PATH];
    FILE *f;
    if (!GetEnvironmentVariableA("FAKEMAPI_OUT", cesta, MAX_PATH)) return;
    f = fopen(cesta, "ab");
    if (!f) return;
    fputs(s, f);
    fclose(f);
}

static void zapisW(const WCHAR *w)
{
    char buf[8192];
    int n;
    if (!w) { zapis("(null)"); return; }
    n = WideCharToMultiByte(CP_UTF8, 0, w, -1, buf, sizeof(buf), NULL, NULL);
    if (n > 0) zapis(buf);
}

ULONG FAR PASCAL MAPISendMailW(LHANDLE s, ULONG_PTR ui, lpMapiMessageW m, FLAGS fl, ULONG r)
{
    ULONG i;
    char t[64];
    sprintf(t, "W|%lu|%lu|", (unsigned long)fl, (unsigned long)m->nRecipCount);
    zapis(t);
    for (i = 0; i < m->nRecipCount; i++) { zapisW(m->lpRecips[i].lpszAddress); zapis(","); }
    zapis("|"); zapisW(m->lpszSubject);
    zapis("|"); zapisW(m->lpszNoteText);
    zapis("|");
    for (i = 0; i < m->nFileCount; i++) { zapisW(m->lpFiles[i].lpszPathName); zapis(","); }
    zapis("\n#\n");
    return SUCCESS_SUCCESS;
}

ULONG FAR PASCAL MAPISendMail(LHANDLE s, ULONG_PTR ui, lpMapiMessage m, FLAGS fl, ULONG r)
{
    zapis("A|");
    zapis(m->lpszSubject ? m->lpszSubject : "(null)");
    zapis("\n#\n");
    return SUCCESS_SUCCESS;
}

BOOL WINAPI DllMain(HINSTANCE h, DWORD d, LPVOID r) { return TRUE; }
