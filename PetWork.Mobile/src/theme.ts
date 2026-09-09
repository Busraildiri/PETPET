import { Appearance, StyleSheet } from 'react-native';

export type ThemeMode = 'light' | 'dark';

const lightColors = {
  background: '#FFF9F2', card: '#FFFCF8', primary: '#6D4A65', primaryDark: '#56384F',
  peach: '#EFA58F', peachSoft: '#FBE9E2', sage: '#A8BFA5', sageSoft: '#E8F0E6',
  yellow: '#F3D58A', yellowSoft: '#FFF4D7', lilacSoft: '#EFE4F5', text: '#332B2D',
  muted: '#756A6D', border: '#EDE3DD', white: '#FFFFFF', danger: '#A34242',
};

const darkColors: typeof lightColors = {
  background: '#1D181C', card: '#292228', primary: '#E0BFD7', primaryDark: '#EBD3E4',
  peach: '#E99A85', peachSoft: '#442D2B', sage: '#A8C6A4', sageSoft: '#29372C',
  yellow: '#E0BE6D', yellowSoft: '#3B3220', lilacSoft: '#34283B', text: '#F6EFF2',
  muted: '#CBBFC5', border: '#4B3E45', white: '#FFFFFF', danger: '#F08A80',
};

let mode: ThemeMode = 'light';
let revision = 0;

export function setThemeMode(next: ThemeMode) {
  if (mode !== next) {
    mode = next;
    revision += 1;
  }
  Appearance.setColorScheme(next);
}

export function getThemeMode() { return mode; }

export const colors = new Proxy(lightColors, {
  get(_target, property: keyof typeof lightColors) {
    return (mode === 'dark' ? darkColors : lightColors)[property];
  },
});

const lightBackgrounds = new Set(['#FFF9F2', '#FFF9F3', '#FFF8F4']);
const lightCards = new Set(['#FFFCF8', '#FFFCF9', '#FFFCFA', '#FFFBFF', '#FBFFF9']);
const darkPaletteValues = new Set(Object.values(darkColors).map(value => value.toUpperCase()).filter(value => value !== '#FFFFFF'));

function themedColor(value: string, property?: string) {
  if (mode === 'light' || !/^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(value)) return value;
  const upper = value.toUpperCase();
  const rgb = upper.slice(0, 7);
  const alpha = upper.length === 9 ? upper.slice(7) : '';
  if (darkPaletteValues.has(rgb)) return value;
  if (lightBackgrounds.has(rgb)) return darkColors.background + alpha;
  if (lightCards.has(rgb)) return darkColors.card + alpha;
  if (alpha) return value;
  if (rgb === '#FFFFFF') return property === 'color' || property === 'tintColor' ? value : darkColors.card + alpha;

  const red = parseInt(rgb.slice(1, 3), 16) / 255;
  const green = parseInt(rgb.slice(3, 5), 16) / 255;
  const blue = parseInt(rgb.slice(5, 7), 16) / 255;
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
  const saturation = max === 0 ? 0 : (max - min) / max;
  if (luminance > 0.82) return '#3B3036' + alpha;
  if (luminance > 0.68 && saturation < 0.28) return darkColors.border + alpha;
  if (luminance < 0.48 && saturation < 0.28) return (luminance < 0.27 ? darkColors.text : darkColors.muted) + alpha;
  return value;
}

function transformThemeValue(value: unknown, property?: string): unknown {
  if (typeof value === 'string') return themedColor(value, property);
  if (Array.isArray(value)) return value.map(item => transformThemeValue(item, property));
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, transformThemeValue(item, key)]));
  }
  return value;
}

export function createThemedStyles<T extends StyleSheet.NamedStyles<T>>(factory: () => T): T {
  let cachedRevision = -1;
  let cached: T;
  return new Proxy({} as T, {
    get(_target, property: string | symbol) {
      if (cachedRevision !== revision) {
        cached = StyleSheet.create(transformThemeValue(factory()) as T);
        cachedRevision = revision;
      }
      return cached[property as keyof T];
    },
  });
}

const lightShadow = {
  shadowColor: '#4D3D45', shadowOffset: { width: 0, height: 6 }, shadowOpacity: 0.09,
  shadowRadius: 14, elevation: 3,
};

export const shadow = new Proxy(lightShadow, {
  get(target, property: keyof typeof lightShadow) {
    if (property === 'shadowColor' && mode === 'dark') return '#000000';
    if (property === 'shadowOpacity' && mode === 'dark') return 0.16;
    if (property === 'shadowRadius' && mode === 'dark') return 10;
    if (property === 'elevation' && mode === 'dark') return 1;
    return target[property];
  },
});
