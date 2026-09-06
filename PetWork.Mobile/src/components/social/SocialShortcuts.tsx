import { Ionicons } from '@expo/vector-icons';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, shadow } from '../../theme';

type CommunityProps = {
  onOpenAdoption: () => void;
  onOpenReviews: () => void;
  onOpenLost: () => void;
};

export function CommunityShortcuts({ onOpenAdoption, onOpenReviews, onOpenLost }: CommunityProps) {
  return (
    <View>
      <Text style={styles.heading}>Topluluğu keşfet</Text>
      <View style={styles.pair}>
        <Shortcut title="Sahiplendirme" subtitle="Bir yuvaya aracılık et" icon="home-outline" color={colors.sageSoft} onPress={onOpenAdoption} />
        <Shortcut title="Pati Denedi" subtitle="Mama deneyimleri" icon="nutrition-outline" color={colors.yellowSoft} onPress={onOpenReviews} />
      </View>
      <Pressable onPress={onOpenLost} style={({ pressed }) => [styles.lostCard, pressed && styles.pressed]}>
        <View style={styles.lostIcon}><Ionicons name="location" size={24} color={colors.white} /></View>
        <View style={styles.flex}><Text style={styles.lostTitle}>Kayıp Patiler</Text><Text style={styles.subtitle}>Yakınındaki ilanlara göz at veya bildirim oluştur.</Text></View>
        <Ionicons name="chevron-forward" size={20} color={colors.primary} />
      </Pressable>
    </View>
  );
}

export function NearbyShortcuts({ onOpenNearby }: { onOpenNearby: () => void }) {
  const items = [
    ['Veterinerler', 'medical-outline'], ['Pet Kuaförleri', 'cut-outline'],
    ['Pet Otelleri', 'bed-outline'], ['Park ve Oyun Alanları', 'leaf-outline'],
  ] as const;
  return (
    <View>
      <Text style={styles.heading}>Yakınındakiler</Text>
      <Text style={styles.hint}>Konumun yalnızca sen istediğinde kullanılır.</Text>
      <View style={styles.grid}>{items.map(([title, icon], index) => (
        <Pressable key={title} onPress={onOpenNearby} style={({ pressed }) => [styles.nearbyCard, { backgroundColor: index % 2 ? colors.sageSoft : colors.lilacSoft }, pressed && styles.pressed]}>
          <Ionicons name={icon} size={27} color={index % 2 ? '#4E7458' : colors.primary} />
          <Text style={styles.nearbyTitle}>{title}</Text><Text style={styles.nearbyLink}>Listeyi gör →</Text>
        </Pressable>
      ))}</View>
      <View style={styles.privacy}><Ionicons name="lock-closed-outline" size={15} color="#4E7458" /><Text style={styles.privacyText}>Kesin konumun diğer kullanıcılara gösterilmez.</Text></View>
    </View>
  );
}

function Shortcut({ title, subtitle, icon, color, onPress }: { title: string; subtitle: string; icon: keyof typeof Ionicons.glyphMap; color: string; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.shortcut, { backgroundColor: color }, pressed && styles.pressed]}>
    <View style={styles.shortcutIcon}><Ionicons name={icon} size={24} color={colors.primary} /></View>
    <Text style={styles.shortcutTitle}>{title}</Text><Text style={styles.shortcutSubtitle}>{subtitle}</Text>
  </Pressable>;
}

const styles = StyleSheet.create({
  heading: { color: colors.text, fontFamily: 'serif', fontSize: 22, fontWeight: '700', marginTop: 25, marginBottom: 5 },
  hint: { color: colors.muted, fontSize: 11, marginBottom: 14 }, pair: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 9 },
  shortcut: { width: '48.4%', minHeight: 145, borderRadius: 21, padding: 16, ...shadow },
  shortcutIcon: { width: 45, height: 45, borderRadius: 23, backgroundColor: '#FFFCF8B8', alignItems: 'center', justifyContent: 'center' },
  shortcutTitle: { color: colors.text, fontSize: 14, fontWeight: '900', marginTop: 15 }, shortcutSubtitle: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 4 },
  lostCard: { flexDirection: 'row', alignItems: 'center', gap: 12, minHeight: 86, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, borderRadius: 21, padding: 15, marginTop: 12 },
  lostIcon: { width: 47, height: 47, borderRadius: 24, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary }, flex: { flex: 1 },
  lostTitle: { color: colors.text, fontSize: 15, fontWeight: '900' }, subtitle: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 3 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', rowGap: 12 },
  nearbyCard: { width: '48.4%', minHeight: 126, borderRadius: 20, padding: 16, justifyContent: 'space-between', ...shadow },
  nearbyTitle: { color: colors.text, fontSize: 14, lineHeight: 18, fontWeight: '900', marginTop: 13 }, nearbyLink: { color: colors.primary, fontSize: 10, fontWeight: '800', marginTop: 7 },
  privacy: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, marginTop: 17 }, privacyText: { color: colors.muted, fontSize: 10 }, pressed: { opacity: 0.82, transform: [{ scale: 0.985 }] },
});
