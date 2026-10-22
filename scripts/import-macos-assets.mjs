import { copyFile, mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { execFile } from "node:child_process";
import { promisify } from "node:util";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, "..");
const macRoot = resolve(projectRoot, "..", "pokeTab", "pokeTab");
const windowsAssets = resolve(projectRoot, "src", "PokeDock.Windows", "Assets");
const run = promisify(execFile);

const source = await readFile(resolve(macRoot, "PokemonCatalog.swift"), "utf8");
const namesMatch = source.match(/private static let names = """\n([\s\S]*?)\n\s*"""\.split/);
const typesMatch = source.match(/private static let typeRows = """\n\s*([^\n]+)\n\s*"""\.split/);
if (!namesMatch || !typesMatch) {
  throw new Error("Não foi possível interpretar PokemonCatalog.swift.");
}

const names = namesMatch[1].split("\n").map((name) => name.trim()).filter(Boolean);
const typeRows = typesMatch[1].trim().split("|").map((row) => row.trim().split(/\s+/));
const parseSet = (name) => {
  const match = source.match(new RegExp(`private static let ${name}: Set<Int> = \\[([^\\]]+)\\]`));
  if (!match) throw new Error(`Conjunto ${name} não encontrado.`);
  return new Set(match[1].split(",").map((value) => Number(value.trim())));
};
const rare = parseSet("rareIDs");
const uncommon = parseSet("uncommonIDs");
const special = new Set(Array.from({ length: 8 }, (_, index) => index + 144));
const heavy = new Set([16, 19, 21, 41, 72]);
const medium = new Set([10, 13, 29, 32, 43, 50, 69, 74, 109, 129]);

if (names.length !== 151 || typeRows.length !== 151) {
  throw new Error(`Catálogo incompleto: ${names.length} nomes e ${typeRows.length} tipos.`);
}

const catalog = names.map((name, index) => {
  const id = index + 1;
  const rarity = special.has(id) ? "special" : rare.has(id) ? "rare" : uncommon.has(id) ? "uncommon" : "common";
  return {
    id,
    name,
    rarity,
    types: typeRows[index],
    encounterWeight: heavy.has(id) ? 5 : medium.has(id) ? 2 : 1
  };
});

await mkdir(resolve(windowsAssets, "Data"), { recursive: true });
await mkdir(resolve(windowsAssets, "Pokemon"), { recursive: true });
await mkdir(resolve(windowsAssets, "App"), { recursive: true });
await writeFile(resolve(windowsAssets, "Data", "pokemon.json"), `${JSON.stringify(catalog, null, 2)}\n`);

for (const species of catalog) {
  const sourceSprite = resolve(macRoot, "Assets.xcassets", `pokemon-${species.id}.imageset`, "sprite.png");
  await copyFile(sourceSprite, resolve(windowsAssets, "Pokemon", `${species.id}.png`));
}

const iconDirectory = resolve(macRoot, "Assets.xcassets", "AppIcon.appiconset");
await copyFile(resolve(iconDirectory, "grass-64.png"), resolve(windowsAssets, "App", "Square44x44Logo.png"));
await copyFile(resolve(iconDirectory, "grass-256.png"), resolve(windowsAssets, "App", "Square150x150Logo.png"));
await copyFile(resolve(iconDirectory, "grass-64.png"), resolve(windowsAssets, "App", "StoreLogo.png"));
await run("sips", ["-z", "44", "44", resolve(windowsAssets, "App", "Square44x44Logo.png")]);
await run("sips", ["-z", "150", "150", resolve(windowsAssets, "App", "Square150x150Logo.png")]);
await run("sips", ["-z", "50", "50", resolve(windowsAssets, "App", "StoreLogo.png")]);

console.log(`Importados ${catalog.length} Pokémon e seus sprites.`);
