import { Ionicons } from '@expo/vector-icons';
import { StatusBar } from 'expo-status-bar';
import { Alert, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors, shadow } from '../theme';

export function AccountScreen({ username, onBack, onLogout }: { username: string; onBack: () => void; onLogout: () => void }) {
  const confirmLogout = () => Alert.alert(
    'Çıkış yapmak istiyor musun?',
    'Bu cihazdaki Pet’im oturumun kapatılacak. Hesabın ve içeriklerin silinmeyecek.',
    [
      { text: 'Vazgeç', style: 'cancel' },
      { text: 'Çıkış Yap', style: 'destructive', onPress: onLogout },
    ],
  );

  return <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
    <StatusBar style="dark" />
    <View style={styles.header}>
      <Pressable onPress={onBack} accessibilityLabel="Ana sayfaya dön" style={styles.roundButton}><Ionicons name="arrow-back" size={22} color={colors.primary} /></Pressable>
      <Text style={styles.headerTitle}>Hesabım</Text><View style={styles.roundButton}><Ionicons name="paw" size={19} color={colors.peach} /></View>
    </View>

    <View style={styles.profileCard}>
      <View style={styles.avatar}><Text style={styles.avatarText}>{username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View>
      <Text style={styles.username}>{username}</Text><Text style={styles.memberText}>PetWork topluluk üyesi</Text>
      <View style={styles.activeBadge}><Ionicons name="shield-checkmark" size={15} color="#41604A" /><Text style={styles.activeText}>Oturum güvenli</Text></View>
    </View>

    <View style={styles.menuCard}>
      <MenuRow icon="person-outline" title="Profil bilgileri" subtitle="Profilini ve iletişim bilgilerini düzenle" />
      <MenuRow icon="paw-outline" title="Patilerim" subtitle="Evcil hayvan profillerini yönet" />
      <MenuRow icon="notifications-outline" title="Bildirimler" subtitle="Bildirim tercihlerini düzenle" last />
    </View>

    <View style={styles.infoCard}><Ionicons name="information-circle-outline" size={22} color={colors.primary} /><Text style={styles.infoText}>Çıkış yapmak hesabını silmez. Aynı hesapla web sitesinden ve mobil uygulamadan yeniden giriş yapabilirsin.</Text></View>

    <Pressable onPress={confirmLogout} accessibilityRole="button" accessibilityLabel="Hesaptan çıkış yap" style={({ pressed }) => [styles.logoutButton, pressed && styles.pressed]}>
      <Ionicons name="log-out-outline" size={21} color="#9B463B" /><Text style={styles.logoutText}>Çıkış Yap</Text>
    </Pressable>
  </ScrollView>;
}

function MenuRow({ icon, title, subtitle, last = false }: { icon: keyof typeof Ionicons.glyphMap; title: string; subtitle: string; last?: boolean }) {
  return <View style={[styles.menuRow, last && styles.menuRowLast]}><View style={styles.menuIcon}><Ionicons name={icon} size={21} color={colors.primary} /></View><View style={styles.flexOne}><Text style={styles.menuTitle}>{title}</Text><Text style={styles.menuSubtitle}>{subtitle}</Text></View><Ionicons name="chevron-forward" size={18} color="#AA9DA4" /></View>;
}

const serif = Platform.select({ ios: 'Georgia', android: 'serif', default: 'serif' });
const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background }, content: { flexGrow: 1, paddingTop: Platform.OS === 'ios' ? 56 : 28, paddingHorizontal: 20, paddingBottom: 112, width: '100%', maxWidth: 620, alignSelf: 'center' },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }, roundButton: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center', ...shadow }, headerTitle: { color: colors.text, fontFamily: serif, fontSize: 22, fontWeight: '700' },
  profileCard: { alignItems: 'center', backgroundColor: colors.card, borderRadius: 26, padding: 24, marginTop: 24, borderWidth: 1, borderColor: colors.border, ...shadow }, avatar: { width: 82, height: 82, borderRadius: 41, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', borderWidth: 5, borderColor: colors.lilacSoft }, avatarText: { color: colors.white, fontFamily: serif, fontSize: 36, fontWeight: '700' },
  username: { color: colors.text, fontFamily: serif, fontSize: 26, fontWeight: '700', marginTop: 13 }, memberText: { color: colors.muted, fontSize: 11, marginTop: 4 }, activeBadge: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.sageSoft, paddingHorizontal: 11, paddingVertical: 7, borderRadius: 15, marginTop: 13 }, activeText: { color: '#41604A', fontSize: 10, fontWeight: '800' },
  menuCard: { backgroundColor: colors.card, borderRadius: 22, marginTop: 16, paddingHorizontal: 16, borderWidth: 1, borderColor: colors.border }, menuRow: { minHeight: 76, flexDirection: 'row', alignItems: 'center', gap: 12, borderBottomWidth: 1, borderBottomColor: colors.border }, menuRowLast: { borderBottomWidth: 0 }, menuIcon: { width: 42, height: 42, borderRadius: 21, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, flexOne: { flex: 1 }, menuTitle: { color: colors.text, fontSize: 13, fontWeight: '800' }, menuSubtitle: { color: colors.muted, fontSize: 9, marginTop: 4 },
  infoCard: { flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: colors.sageSoft, borderRadius: 17, padding: 15, marginTop: 16 }, infoText: { flex: 1, color: '#56635A', fontSize: 10, lineHeight: 15 },
  logoutButton: { minHeight: 53, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 9, backgroundColor: colors.peachSoft, borderWidth: 1, borderColor: colors.peach, borderRadius: 18, marginTop: 18 }, logoutText: { color: '#8E4036', fontSize: 13, fontWeight: '900' }, pressed: { opacity: 0.78, transform: [{ scale: 0.99 }] },
});
