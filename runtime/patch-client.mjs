import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname } from 'node:path';

const [sourcePath, outputPath] = process.argv.slice(2);
if (!sourcePath || !outputPath) throw new Error('Usage: node patch-client.mjs <source-main.js> <output-main.js>');
let code = await readFile(sourcePath, 'utf8');

const appStart = code.indexOf('function xw({scene:e,theme:t}){');
const appEnd = code.indexOf('var x9=', appStart);
if (appStart < 0 || appEnd < 0) throw new Error('Unsupported excalidraw-edit client: App wrapper marker not found');
const appReplacement = `let LM=null;function saveManagedLibrary(e){clearTimeout(LM),LM=setTimeout(()=>{fetch("/library",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({type:"excalidrawlib",version:2,source:"ExcalidrawManager",libraryItems:e})}).catch(t=>console.error("Library save failed:",t))},300)}function xw({scene:e,theme:t,libraryItems:n}){return(0,ww.jsx)("div",{style:{width:"100vw",height:"100vh"},children:(0,ww.jsx)(Sw,{initialData:{elements:e.elements||[],appState:{...e.appState,theme:t},files:e.files||{},libraryItems:n||[]},onChange:zG,onLibraryChange:saveManagedLibrary,libraryReturnUrl:window.location.origin+window.location.pathname})})}`;
code = code.slice(0, appStart) + appReplacement + code.slice(appEnd);

const bootStart = code.indexOf('async function BG(){');
const bootCall = 'BG().catch(console.error);';
const bootEnd = code.indexOf(bootCall, bootStart);
if (bootStart < 0 || bootEnd < 0) throw new Error('Unsupported excalidraw-edit client: bootstrap marker not found');
const bootReplacement = `async function BG(){window.name||(window.name="ExcalidrawManager"+location.port);let importParams=new URLSearchParams(location.hash.slice(1)),importUrl=importParams.get("addLibrary");if(importUrl)try{let importResponse=await fetch("/library/import",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({url:importUrl})});if(!importResponse.ok)throw new Error(await importResponse.text());history.replaceState(null,"",location.pathname+location.search)}catch(importError){console.error("Library import failed:",importError)}let[e,t,n]=await Promise.all([fetch("/meta"),fetch("/data"),fetch("/library")]),a=await e.json(),l=await t.json(),i=await n.json(),r=a.fileName.replace(/\\.excalidraw$/," ").trim();document.title=r+" - excalidraw-edit";let s=window.matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light",u=a.theme!=="system"?a.theme:l.appState?.theme||s;await document.fonts.ready,(0,w9.createRoot)(document.getElementById("app")).render((0,x9.jsx)(xw,{scene:l,theme:u,libraryItems:i.libraryItems||[]}));a.formulaEditorUrl&&import("/formula-overlay.mjs").then(e=>e.startFormulaOverlay(a.formulaEditorUrl)).catch(e=>console.error("Formula overlay failed:",e))}BG().catch(console.error);`;
code = code.slice(0, bootStart) + bootReplacement + code.slice(bootEnd + bootCall.length);

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, code);
console.log(`Patched Excalidraw client: ${outputPath}`);
