import { Ionicons } from '@expo/vector-icons';
import { useState } from 'react';
import {
  Alert, Platform, Pressable, ScrollView, StatusBar as NativeStatusBar, StyleSheet, Text, View,
} from 'react-native';
import { CommunityShortcuts, NearbyShortcuts } from '../components/social/SocialShortcuts';
import { PostCard } from '../components/social/PostCard';
import { mockSocialPosts } from '../data/mockSocialPosts';
import { colors, shadow } from '../theme';
import type { SocialPost, SocialTab } from '../types/social';

type Props = {
  onOpenNearby: () => void;
  onOpenAdoption: () => void;
  onOpenReviews: () => void;
  onOpenLost: () => void;
};

const tabs: { key: SocialTab; label: string }[] = [
  { key: 'posts', label: 'Gönderiler' },
  { key: 'questions', label: 'Soru-Cevap' },
  { key: 'nearby', label: 'Yakınımda' },
];

export function PatiSocialScreen({ onOpenNearby, onOpenAdoption, onOpenReviews, onOpenLost }: Props) {
  const [activeTab, setActiveTab] = useState<SocialTab>('posts');

  const openPendingFeature = (action: string) => Alert.alert(
    `${action} hazırlanıyor`,
    'Admin oturumuyla devam ediyorsun. Bu işlem gerçek mobil oturum ve gönderi API’si bağlandığında kaydedilecek.',
    [{ text: 'Tamam', style: 'cancel' }],
  );

  const openComments = (post: SocialPost) => Alert.alert(
    `${post.petName} · Yorumlar`,
    'Yorum ekranı bir sonraki adımda gerçek gönderi servisiyle bağlanacak.',
  );

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
      <View style={styles.header}>
        <View style={styles.headerIcon}><Ionicons name="paw" size={25} color={colors.peach} /></View>
        <View style={styles.headerCopy}>
          <Text style={styles.title}>PatiSosyal</Text>
          <Text style={styles.subtitle}>Bilgi, deneyim ve güvenli dayanışma</Text>
        </View>
        <Pressable onPress={() => Alert.alert('admin', 'Yönetici hesabıyla önizleme yapıyorsun.')} style={styles.accountButton} accessibilityLabel="Admin hesabı">
          <View style={styles.accountAvatar}><Text style={styles.accountAvatarText}>A</Text></View><Text style={styles.accountText}>admin</Text>
        </Pressable>
      </View>

      <View style={styles.tabs}>{tabs.map(tab => (
        <Pressable key={tab.key} onPress={() => setActiveTab(tab.key)} style={[styles.tab, activeTab === tab.key && styles.activeTab]} accessibilityRole="tab" accessibilityState={{ selected: activeTab === tab.key }}>
          <Text style={[styles.tabText, activeTab === tab.key && styles.activeTabText]}>{tab.label}</Text>
        </Pressable>
      ))}</View>

      {activeTab === 'posts' ? (
        <>
          <Pressable onPress={() => openPendingFeature('Gönderi oluşturma')} style={({ pressed }) => [styles.composer, pressed && styles.pressed]}>
            <View style={styles.composerAvatar}><Text style={styles.composerAvatarText}>A</Text></View>
            <View style={styles.composerCopy}><Text style={styles.composerTitle}>admin olarak paylaş</Text><Text style={styles.composerText}>Fotoğraf, deneyim veya küçük bir mutluluk…</Text></View>
            <View style={styles.photoButton}><Ionicons name="images-outline" size={21} color="#4E7458" /></View>
          </Pressable>

          <CommunityShortcuts onOpenAdoption={onOpenAdoption} onOpenReviews={onOpenReviews} onOpenLost={onOpenLost} />

          <View style={styles.sectionHeader}>
            <View><Text style={styles.sectionTitle}>Topluluk akışı</Text><Text style={styles.sectionHint}>PetWork’ten başlangıç paylaşımları</Text></View>
            <Pressable onPress={() => openPendingFeature('Gönderi oluşturma')} style={styles.addButton}><Ionicons name="add" size={19} color={colors.white} /><Text style={styles.addText}>Paylaş</Text></Pressable>
          </View>

          {mockSocialPosts.map(post => <PostCard key={post.id} post={post} onComment={openComments} onReport={() => openPendingFeature('İçerik bildirimi')} />)}
        </>
      ) : null}

      {activeTab === 'questions' ? <QuestionPreview onAsk={() => openPendingFeature('Soru oluşturma')} /> : null}
      {activeTab === 'nearby' ? <NearbyShortcuts onOpenNearby={onOpenNearby} /> : null}
    </ScrollView>
  );
}

function QuestionPreview({ onAsk }: { onAsk: () => void }) {
  const questions = [
    ['Beslenme', 'Yeni mamaya geçişi nasıl daha rahat yapabilirim?', 'Soru kartı örneği'],
    ['Davranış', 'Kedimin gece hareketliliğini nasıl düzenleyebilirim?', 'Soru kartı örneği'],
  ];
  return <View>
    <View style={styles.questionIntro}><Ionicons name="chatbubbles-outline" size={29} color={colors.primary} /><View style={styles.headerCopy}><Text style={styles.questionIntroTitle}>Birlikte öğrenelim</Text><Text style={styles.questionIntroText}>Webdeki gerçek sorular mobil API ile bu alana bağlanacak.</Text></View></View>
    {questions.map(([category, title, meta]) => <View key={title} style={styles.questionCard}>
      <Text style={styles.questionCategory}>{category}</Text><Text style={styles.questionTitle}>{title}</Text>
      <View style={styles.questionFooter}><Text style={styles.questionMeta}>{meta}</Text><Ionicons name="chevron-forward" size={19} color={colors.primary} /></View>
    </View>)}
    <Pressable onPress={onAsk} style={({ pressed }) => [styles.askButton, pressed && styles.pressed]}><Ionicons name="add-circle-outline" size={21} color={colors.white} /><Text style={styles.askText}>Yeni soru sor</Text></Pressable>
  </View>;
}

const statusInset = Platform.OS === 'android' ? NativeStatusBar.currentHeight ?? 24 : 50;
const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background }, content: { paddingTop: statusInset + 12, paddingHorizontal: 19, paddingBottom: 116, width: '100%', maxWidth: 760, alignSelf: 'center' },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12 }, headerIcon: { width: 54, height: 54, borderRadius: 27, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  headerCopy: { flex: 1 }, title: { color: colors.text, fontFamily: 'serif', fontSize: 30, fontWeight: '700', letterSpacing: -0.5 }, subtitle: { color: colors.muted, fontSize: 12, marginTop: 2 },
  accountButton: { minHeight: 42, flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.lilacSoft, borderRadius: 21, paddingLeft: 5, paddingRight: 10 },
  accountAvatar: { width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary }, accountAvatarText: { color: colors.white, fontSize: 13, fontWeight: '900' }, accountText: { color: colors.primary, fontSize: 11, fontWeight: '900' },
  tabs: { flexDirection: 'row', backgroundColor: '#EEE8E9', borderRadius: 20, padding: 5, marginTop: 24, marginBottom: 18 }, tab: { flex: 1, minHeight: 45, borderRadius: 16, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 3 },
  activeTab: { backgroundColor: colors.card, ...shadow }, tabText: { color: colors.muted, fontSize: 11, fontWeight: '800' }, activeTabText: { color: colors.primary },
  composer: { flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 21, padding: 13, ...shadow },
  composerAvatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, composerCopy: { flex: 1 },
  composerAvatarText: { color: colors.primary, fontSize: 16, fontWeight: '900' },
  composerTitle: { color: colors.text, fontSize: 14, fontWeight: '900' }, composerText: { color: colors.muted, fontSize: 10, marginTop: 3 }, photoButton: { width: 39, height: 39, borderRadius: 20, backgroundColor: colors.sageSoft, alignItems: 'center', justifyContent: 'center' },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 28, marginBottom: 14 }, sectionTitle: { color: colors.text, fontFamily: 'serif', fontSize: 23, fontWeight: '700' }, sectionHint: { color: colors.muted, fontSize: 10, marginTop: 3 },
  addButton: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: colors.primary, borderRadius: 17, paddingHorizontal: 13, minHeight: 38 }, addText: { color: colors.white, fontSize: 11, fontWeight: '800' },
  questionIntro: { flexDirection: 'row', alignItems: 'center', gap: 13, backgroundColor: colors.lilacSoft, borderRadius: 21, padding: 17, marginTop: 4, marginBottom: 14 },
  questionIntroTitle: { color: colors.text, fontFamily: 'serif', fontSize: 19, fontWeight: '700' }, questionIntroText: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 3 },
  questionCard: { backgroundColor: colors.card, borderRadius: 20, padding: 17, borderWidth: 1, borderColor: colors.border, marginBottom: 12, ...shadow },
  questionCategory: { color: '#4E7458', backgroundColor: colors.sageSoft, alignSelf: 'flex-start', paddingHorizontal: 9, paddingVertical: 4, borderRadius: 11, overflow: 'hidden', fontSize: 9, fontWeight: '900' },
  questionTitle: { color: colors.text, fontSize: 15, lineHeight: 21, fontWeight: '900', marginTop: 11 }, questionFooter: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 13 }, questionMeta: { color: colors.muted, fontSize: 9 },
  askButton: { minHeight: 50, borderRadius: 18, backgroundColor: colors.primary, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, marginTop: 5 }, askText: { color: colors.white, fontSize: 13, fontWeight: '900' }, pressed: { opacity: 0.8, transform: [{ scale: 0.985 }] },
});
