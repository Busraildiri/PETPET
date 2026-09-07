import { Ionicons } from '@expo/vector-icons';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator, Alert, BackHandler, Platform, Pressable, ScrollView, StatusBar as NativeStatusBar, StyleSheet, Text, TextInput, View, useWindowDimensions,
} from 'react-native';
import {
  createQuestion, createSocialPost, deleteSocialPost, getQuestionDetail, getQuestions, getSocialPosts,
  mediaUrl, postQuestionAnswer, reportSocialPost, QuestionAnswer, QuestionDetail, QuestionsResponse,
  QuestionSummary, type SocialPostPayload,
} from '../api';
import { CommentsModal } from '../components/social/CommentsModal';
import { CreatePostModal, type SelectedPostImage } from '../components/social/CreatePostModal';
import { CreateQuestionModal } from '../components/social/CreateQuestionModal';
import { CommunityShortcuts, NearbyShortcuts, type NearbyCategory } from '../components/social/SocialShortcuts';
import { PostCard } from '../components/social/PostCard';
import { mockSocialPosts } from '../data/mockSocialPosts';
import { colors, shadow } from '../theme';
import type { SocialPost, SocialTab } from '../types/social';

type Props = {
  initialTab?: SocialTab;
  username: string | null;
  authToken: string | null;
  onOpenAccount: () => void;
  onLogin: () => void;
  onOpenNearby: (category: NearbyCategory) => void;
  onOpenAdoption: () => void;
  onOpenReviews: () => void;
  onOpenLost: () => void;
};

const tabs: { key: SocialTab; label: string }[] = [
  { key: 'posts', label: 'Gönderiler' },
  { key: 'questions', label: 'Soru-Cevap' },
  { key: 'nearby', label: 'Yakınımda' },
];

function toSocialPost(post: SocialPostPayload): SocialPost {
  return {
    id: `api-${post.id}`,
    serverId: post.id,
    ownerName: post.isAdmin ? 'PetWork Yönetimi' : post.username,
    username: `@${post.username}`,
    petName: post.username,
    petType: 'Topluluk',
    publishedAt: new Date(post.createdAt).toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }),
    body: post.body,
    image: post.imagePath ? { uri: mediaUrl(post.imagePath) } : undefined,
    tags: post.tags?.split(',').map(tag => tag.trim()).filter(Boolean) ?? [],
    isAdmin: post.isAdmin,
    commentCount: post.commentCount,
  };
}

export function PatiSocialScreen({ initialTab = 'posts', username, authToken, onOpenAccount, onLogin, onOpenNearby, onOpenAdoption, onOpenReviews, onOpenLost }: Props) {
  const { width } = useWindowDimensions();
  const [activeTab, setActiveTab] = useState<SocialTab>(initialTab);
  const [posts, setPosts] = useState<SocialPost[]>([]);
  const [postsLoading, setPostsLoading] = useState(true);
  const [postsError, setPostsError] = useState<string | null>(null);
  const [composerOpen, setComposerOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [composerError, setComposerError] = useState<string | null>(null);
  const [commentPost, setCommentPost] = useState<SocialPost | null>(null);
  const [questionComposerOpen, setQuestionComposerOpen] = useState(false);
  const [questionSubmitting, setQuestionSubmitting] = useState(false);
  const [questionComposerError, setQuestionComposerError] = useState<string | null>(null);
  const [questionsRevision, setQuestionsRevision] = useState(0);
  const displayName = username ?? 'Misafir';
  const avatarLetter = username?.charAt(0).toLocaleUpperCase('tr-TR') ?? '?';

  const loadPosts = useCallback(async (signal?: AbortSignal) => {
    setPostsError(null);
    try {
      setPosts((await getSocialPosts(signal)).map(toSocialPost));
    } catch (reason) {
      if (signal?.aborted) return;
      setPostsError(reason instanceof Error ? reason.message : 'Topluluk akışı yüklenemedi.');
    } finally {
      if (!signal?.aborted) setPostsLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadPosts(controller.signal);
    return () => controller.abort();
  }, [loadPosts]);

  const requireLogin = (message: string) => Alert.alert('Giriş yapmalısın', message, [
    { text: 'Vazgeç', style: 'cancel' },
    { text: 'Giriş Yap', onPress: onLogin },
  ]);

  const openComposer = () => {
    if (!username || !authToken) return requireLogin('Paylaşım yapmak için Pet’im hesabına giriş yap.');
    setComposerError(null);
    setComposerOpen(true);
  };

  const submitPost = async (body: string, tags: string[], image?: SelectedPostImage) => {
    if (!authToken || body.length < 2) return;
    setSubmitting(true);
    setComposerError(null);
    try {
      const created = await createSocialPost(authToken, body, tags, image);
      setPosts(current => [toSocialPost(created), ...current]);
      setComposerOpen(false);
      Alert.alert('Paylaşıldı', 'Gönderin PatiSosyal akışına eklendi ve 10 XP kazandın.');
    } catch (reason) {
      setComposerError(reason instanceof Error ? reason.message : 'Paylaşım gönderilemedi.');
    } finally { setSubmitting(false); }
  };

  const openQuestionComposer = () => {
    if (!username || !authToken) return requireLogin('Soru sormak için Pet’im hesabına giriş yap.');
    setQuestionComposerError(null);
    setQuestionComposerOpen(true);
  };

  const submitQuestion = async (title: string, content: string, category: string) => {
    if (!authToken) return;
    setQuestionSubmitting(true);
    setQuestionComposerError(null);
    try {
      await createQuestion(authToken, title, content, category);
      setQuestionComposerOpen(false);
      setQuestionsRevision(current => current + 1);
      Alert.alert('Sorun yayınlandı', 'Sorun web ve mobil topluluğa eklendi. 10 XP kazandın.');
    } catch (reason) {
      setQuestionComposerError(reason instanceof Error ? reason.message : 'Soru gönderilemedi.');
    } finally { setQuestionSubmitting(false); }
  };

  const openRealComments = (post: SocialPost) => {
    if (!post.serverId) return Alert.alert('Başlangıç paylaşımı', 'Bu arşiv paylaşımı yorum kabul etmiyor.');
    setCommentPost(post);
  };

  const commentAdded = (postId: number) => {
    setPosts(current => current.map(post => post.serverId === postId ? { ...post, commentCount: post.commentCount + 1 } : post));
    setCommentPost(current => current?.serverId === postId ? { ...current, commentCount: current.commentCount + 1 } : current);
  };

  const sendReport = async (post: SocialPost, reason: string) => {
    if (!authToken || !post.serverId) return;
    try { Alert.alert('Bildirimin alındı', await reportSocialPost(authToken, post.serverId, reason)); }
    catch (error) { Alert.alert('Bildirim gönderilemedi', error instanceof Error ? error.message : 'Lütfen tekrar dene.'); }
  };

  const openReportReasons = (post: SocialPost) => Alert.alert('Bildirim nedeni', 'Bu gönderiyi neden bildirmek istiyorsun?', [
    { text: 'Spam', onPress: () => sendReport(post, 'Spam veya tekrarlanan içerik') },
    { text: 'Yanıltıcı / zararlı', onPress: () => sendReport(post, 'Yanıltıcı veya hayvan sağlığına zararlı içerik') },
    { text: 'Uygunsuz içerik', onPress: () => sendReport(post, 'Uygunsuz veya rahatsız edici içerik') },
    { text: 'Vazgeç', style: 'cancel' },
  ]);

  const confirmDelete = (post: SocialPost) => Alert.alert('Gönderi silinsin mi?', 'Bu gönderi akıştan kaldırılacak.', [
    { text: 'Vazgeç', style: 'cancel' },
    { text: 'Sil', style: 'destructive', onPress: async () => {
      if (!authToken || !post.serverId) return;
      try {
        await deleteSocialPost(authToken, post.serverId);
        setPosts(current => current.filter(candidate => candidate.serverId !== post.serverId));
        if (commentPost?.serverId === post.serverId) setCommentPost(null);
      } catch (error) { Alert.alert('Gönderi silinemedi', error instanceof Error ? error.message : 'Lütfen tekrar dene.'); }
    } },
  ]);

  const openPostOptions = (post: SocialPost) => {
    if (!post.serverId) return Alert.alert('Başlangıç paylaşımı', 'Bu arşiv paylaşımı için işlem yapılamıyor.');
    if (!username || !authToken) return requireLogin('Gönderi işlemleri için hesabına giriş yap.');
    Alert.alert('Gönderi işlemleri', 'Yapmak istediğin işlemi seç.', [
      { text: 'Gönderiyi bildir', onPress: () => openReportReasons(post) },
      { text: 'Gönderiyi sil', style: 'destructive', onPress: () => confirmDelete(post) },
      { text: 'Vazgeç', style: 'cancel' },
    ]);
  };

  return (
    <View style={styles.screen}>
    <ScrollView style={styles.screen} contentContainerStyle={[styles.content, activeTab === 'questions' && styles.contentWithFloatingAction]} showsVerticalScrollIndicator={false} keyboardShouldPersistTaps="handled">
      <View style={styles.header}>
        <View style={styles.headerIcon}><Ionicons name="paw" size={25} color={colors.peach} /></View>
        <View style={styles.headerCopy}>
          <Text style={styles.title}>PatiSosyal</Text>
          <Text style={styles.subtitle}>Bilgi, deneyim ve güvenli dayanışma</Text>
        </View>
        <Pressable onPress={username ? onOpenAccount : onLogin} style={styles.accountButton} accessibilityLabel={username ? `${username} hesap menüsünü aç` : 'Giriş yap'}>
          <View style={styles.accountAvatar}><Text style={styles.accountAvatarText}>{avatarLetter}</Text></View><Text style={styles.accountText} numberOfLines={1}>{displayName}</Text>
        </Pressable>
      </View>

      <View style={styles.tabs}>{tabs.map(tab => (
        <Pressable key={tab.key} onPress={() => setActiveTab(tab.key)} style={[styles.tab, activeTab === tab.key && styles.activeTab]} accessibilityRole="tab" accessibilityState={{ selected: activeTab === tab.key }}>
          <Text style={[styles.tabText, activeTab === tab.key && styles.activeTabText]}>{tab.label}</Text>
        </Pressable>
      ))}</View>

      {activeTab === 'posts' ? (
        <>
          <CommunityShortcuts onOpenAdoption={onOpenAdoption} onOpenReviews={onOpenReviews} onOpenLost={onOpenLost} />

          <View style={styles.sectionHeader}>
            <View><Text style={styles.sectionTitle}>Topluluk akışı</Text><Text style={styles.sectionHint}>Topluluğun gerçek paylaşımları</Text></View>
            <Pressable onPress={openComposer} style={styles.addButton}><Ionicons name="add" size={19} color={colors.white} /><Text style={styles.addText}>Paylaş</Text></Pressable>
          </View>

          {postsLoading ? <View style={styles.postsLoading}><ActivityIndicator color={colors.primary} /><Text style={styles.postsLoadingText}>Paylaşımlar yükleniyor…</Text></View> : null}
          {postsError ? <Pressable onPress={() => { setPostsLoading(true); loadPosts(); }} style={styles.postsError}><Text style={styles.postsErrorText}>{postsError}</Text><Text style={styles.retryText}>Yeniden dene</Text></Pressable> : null}
          {[...posts, ...mockSocialPosts].map(post => <PostCard key={post.id} post={post} onComment={openRealComments} onReport={openPostOptions} />)}
        </>
      ) : null}

      {activeTab === 'questions' ? <QuestionsPanel key={questionsRevision} username={username} authToken={authToken} onLogin={onLogin} onAsk={openQuestionComposer} /> : null}
      {activeTab === 'nearby' ? <NearbyShortcuts onOpenNearby={onOpenNearby} /> : null}
    </ScrollView>
    {activeTab === 'questions' ? <Pressable
      onPress={openQuestionComposer}
      accessibilityRole="button"
      accessibilityLabel="Yeni soru sor"
      style={({ pressed }) => [styles.floatingAskButton, { right: Math.max(19, (width - 760) / 2 + 19) }, pressed && styles.pressed]}
    ><Ionicons name="add-circle-outline" size={21} color={colors.white} /><Text style={styles.floatingAskText}>Yeni soru sor</Text></Pressable> : null}
    <CreatePostModal visible={composerOpen} username={username ?? ''} submitting={submitting} error={composerError} onClose={() => { if (!submitting) setComposerOpen(false); }} onSubmit={submitPost} />
    <CommentsModal visible={commentPost !== null} post={commentPost} token={authToken} username={username} onClose={() => setCommentPost(null)} onLogin={onLogin} onCommentAdded={commentAdded} />
    <CreateQuestionModal visible={questionComposerOpen} username={username ?? ''} submitting={questionSubmitting} error={questionComposerError} onClose={() => { if (!questionSubmitting) setQuestionComposerOpen(false); }} onSubmit={submitQuestion} />
    </View>
  );
}

function QuestionsPanel({ username, authToken, onLogin, onAsk }: { username: string | null; authToken: string | null; onLogin: () => void; onAsk: () => void }) {
  const { width } = useWindowDimensions();
  const [payload, setPayload] = useState<QuestionsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('Tümü');
  const [sort, setSort] = useState<'newest' | 'answered'>('newest');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<QuestionDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const controller = new AbortController();
    try {
      setPayload(await getQuestions(controller.signal));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sorular alınamadı.');
    } finally {
      setLoading(false);
    }
    return () => controller.abort();
  }, []);

  useEffect(() => { void load(); }, [load]);

  useEffect(() => {
    if (selectedId === null) { setDetail(null); return; }
    const controller = new AbortController();
    setDetailLoading(true);
    getQuestionDetail(selectedId, controller.signal)
      .then(setDetail)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'Soru ayrıntısı alınamadı.'))
      .finally(() => setDetailLoading(false));
    return () => controller.abort();
  }, [selectedId]);

  useEffect(() => {
    if (selectedId === null) return;
    const subscription = BackHandler.addEventListener('hardwareBackPress', () => {
      setSelectedId(null);
      setError(null);
      return true;
    });
    return () => subscription.remove();
  }, [selectedId]);

  const questions = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase('tr-TR');
    const filtered = (payload?.items ?? []).filter(item =>
      (category === 'Tümü' || item.category === category) &&
      (!normalized || `${item.title} ${item.excerpt} ${item.username}`.toLocaleLowerCase('tr-TR').includes(normalized)));
    return [...filtered].sort((a, b) => sort === 'answered'
      ? b.answerCount - a.answerCount
      : new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }, [payload, query, category, sort]);

  if (selectedId !== null) return <QuestionDetailView
    detail={detail}
    loading={detailLoading}
    username={username}
    authToken={authToken}
    onLogin={onLogin}
    onAnswerAdded={answer => {
      setDetail(current => current ? { ...current, answers: [...current.answers, answer] } : current);
      setPayload(current => current ? { ...current, items: current.items.map(item => item.id === selectedId ? { ...item, answerCount: item.answerCount + 1 } : item) } : current);
    }}
    onBack={() => { setSelectedId(null); setError(null); }}
  />;

  return <View>
    <Pressable
      onPress={onAsk}
      accessibilityRole="button"
      accessibilityLabel="Topluluğa yeni soru sor"
      style={({ pressed }) => [styles.questionIntro, pressed && styles.pressed]}
    >
      <View style={styles.questionIntroIcon}><Ionicons name="chatbubbles-outline" size={27} color={colors.primary} /></View>
      <View style={styles.headerCopy}><Text style={styles.questionIntroTitle}>Topluluğa sor</Text><Text style={styles.questionIntroText}>Yeni bir soru oluştur veya güncel soruları incele.</Text></View>
      <View style={styles.questionCount}><Text style={styles.questionCountValue}>{payload?.totalCount ?? '—'}</Text><Text style={styles.questionCountLabel}>soru</Text></View>
    </Pressable>

    <View style={styles.questionSearch}>
      <Ionicons name="search-outline" size={19} color={colors.primary} />
      <TextInput value={query} onChangeText={setQuery} placeholder="Sorularda ara…" placeholderTextColor="#94898D" style={styles.questionSearchInput} returnKeyType="search" />
      {query ? <Pressable onPress={() => setQuery('')} hitSlop={10}><Ionicons name="close-circle" size={18} color={colors.muted} /></Pressable> : null}
    </View>

    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.categoryList}>
      {['Tümü', ...(payload?.categories ?? [])].map(item => <Pressable key={item} onPress={() => setCategory(item)} style={[styles.categoryChip, category === item && styles.categoryChipActive]}>
        <Text style={[styles.categoryChipText, category === item && styles.categoryChipTextActive]}>{item}</Text>
      </Pressable>)}
    </ScrollView>

    <View style={styles.questionToolbar}>
      <Text style={styles.resultText}>{questions.length} sonuç</Text>
      <View style={styles.sortGroup}>
        <SortButton label="Yeni" selected={sort === 'newest'} onPress={() => setSort('newest')} />
        <SortButton label="Çok yanıtlanan" selected={sort === 'answered'} onPress={() => setSort('answered')} />
      </View>
    </View>

    {loading ? <View style={styles.questionState}><ActivityIndicator color={colors.primary} /><Text style={styles.stateText}>Sorular getiriliyor…</Text></View> : null}
    {!loading && error ? <View style={styles.questionState}><Ionicons name="cloud-offline-outline" size={28} color={colors.peach} /><Text style={styles.stateTitle}>Sorulara ulaşılamadı</Text><Text style={styles.stateText}>{error}</Text><Pressable onPress={() => void load()} style={styles.retryButton}><Text style={styles.retryText}>Tekrar dene</Text></Pressable></View> : null}
    {!loading && !error && questions.length === 0 ? <View style={styles.questionState}><Ionicons name="search-outline" size={28} color={colors.sage} /><Text style={styles.stateTitle}>Sonuç bulunamadı</Text><Text style={styles.stateText}>Aramayı veya kategori filtresini değiştirebilirsin.</Text></View> : null}

    {!loading && !error ? <View style={styles.questionGrid}>{questions.map(item => <QuestionCard key={item.id} item={item} wide={width >= 700} onPress={() => setSelectedId(item.id)} />)}</View> : null}

  </View>;
}

function SortButton({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  return <Pressable onPress={onPress} style={[styles.sortButton, selected && styles.sortButtonActive]}><Text style={[styles.sortText, selected && styles.sortTextActive]}>{label}</Text></Pressable>;
}

function QuestionCard({ item, wide, onPress }: { item: QuestionSummary; wide: boolean; onPress: () => void }) {
  return <Pressable onPress={onPress} style={({ pressed }) => [styles.questionCard, wide && styles.questionCardWide, pressed && styles.pressed]}>
    <View style={styles.questionCardTop}><Text style={styles.questionCategory}>{item.category}</Text>{item.hasAcceptedAnswer ? <View style={styles.solvedBadge}><Ionicons name="checkmark-circle" size={13} color="#41604A" /><Text style={styles.solvedText}>Yanıtlandı</Text></View> : null}</View>
    <Text style={styles.questionTitle} numberOfLines={2}>{item.title}</Text>
    <Text style={styles.questionExcerpt} numberOfLines={3}>{plainText(item.excerpt)}</Text>
    <View style={styles.authorRow}><View style={styles.authorAvatar}><Text style={styles.authorLetter}>{item.username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View><View style={styles.authorCopy}><Text style={styles.authorName}>{item.username}</Text><Text style={styles.questionMeta}>{formatDate(item.createdAt)}</Text></View></View>
    <View style={styles.questionFooter}><View style={styles.metric}><Ionicons name="chatbubble-ellipses-outline" size={15} color={colors.primary} /><Text style={styles.metricText}>{item.answerCount} yanıt</Text></View><View style={styles.metric}><Ionicons name="eye-outline" size={16} color={colors.muted} /><Text style={styles.metricText}>{item.viewCount}</Text></View><Ionicons name="chevron-forward" size={19} color={colors.primary} /></View>
  </Pressable>;
}

function QuestionDetailView({ detail, loading, username, authToken, onLogin, onAnswerAdded, onBack }: {
  detail: QuestionDetail | null;
  loading: boolean;
  username: string | null;
  authToken: string | null;
  onLogin: () => void;
  onAnswerAdded: (answer: QuestionAnswer) => void;
  onBack: () => void;
}) {
  const [answerText, setAnswerText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const submitAnswer = async () => {
    if (!detail) return;
    if (!username || !authToken) {
      Alert.alert('Giriş yapmalısın', 'Yanıtını paylaşmak için Pet’im hesabına giriş yap.', [
        { text: 'Vazgeç', style: 'cancel' },
        { text: 'Giriş Yap', onPress: onLogin },
      ]);
      return;
    }
    const content = answerText.trim();
    if (content.length < 2) { setSubmitError('Yanıtın en az 2 karakter olmalı.'); return; }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const answer = await postQuestionAnswer(authToken, detail.id, content);
      onAnswerAdded(answer);
      setAnswerText('');
      Alert.alert('Yanıtın yayınlandı', 'Yanıtın web sitesiyle aynı topluluk veritabanına kaydedildi.');
    } catch (reason) {
      setSubmitError(reason instanceof Error ? reason.message : 'Yanıt gönderilemedi.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading || !detail) return <View><Pressable onPress={onBack} style={styles.detailBack}><Ionicons name="arrow-back" size={19} color={colors.primary} /><Text style={styles.detailBackText}>Sorulara dön</Text></Pressable><View style={styles.questionState}><ActivityIndicator color={colors.primary} /><Text style={styles.stateText}>Soru açılıyor…</Text></View></View>;

  return <View>
    <Pressable onPress={onBack} style={styles.detailBack}><Ionicons name="arrow-back" size={19} color={colors.primary} /><Text style={styles.detailBackText}>Sorulara dön</Text></Pressable>
    <View style={styles.detailQuestion}>
      <Text style={styles.questionCategory}>{detail.category}</Text><Text style={styles.detailTitle}>{detail.title}</Text><Text style={styles.detailBody}>{plainText(detail.content)}</Text>
      <View style={styles.detailMeta}><Ionicons name="person-circle-outline" size={19} color={colors.primary} /><Text style={styles.authorName}>{detail.username}</Text><Text style={styles.metaDot}>•</Text><Text style={styles.questionMeta}>{formatDate(detail.createdAt)}</Text><Text style={styles.metaDot}>•</Text><Ionicons name="eye-outline" size={15} color={colors.muted} /><Text style={styles.questionMeta}>{detail.viewCount}</Text></View>
    </View>
    <View style={styles.answersHeading}><Text style={styles.answersTitle}>Yanıtlar</Text><Text style={styles.answersCount}>{detail.answers.length}</Text></View>
    {detail.answers.length ? detail.answers.map(answer => <View key={answer.id} style={[styles.answerCard, answer.isAccepted && styles.acceptedAnswer]}>
      <View style={styles.answerTop}><View style={styles.authorAvatar}><Text style={styles.authorLetter}>{answer.username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View><View style={styles.authorCopy}><Text style={styles.authorName}>{answer.username}</Text><Text style={styles.questionMeta}>{formatDate(answer.createdAt)}</Text></View>{answer.isAccepted ? <View style={styles.solvedBadge}><Ionicons name="checkmark-circle" size={13} color="#41604A" /><Text style={styles.solvedText}>Kabul edildi</Text></View> : null}</View>
      <Text style={styles.answerBody}>{plainText(answer.content)}</Text><View style={styles.answerScore}><Ionicons name="paw-outline" size={14} color={colors.primary} /><Text style={styles.metricText}>{answer.score} puan</Text></View>
    </View>) : <View style={styles.questionState}><Ionicons name="chatbubble-outline" size={27} color={colors.sage} /><Text style={styles.stateTitle}>Henüz yanıt yok</Text><Text style={styles.stateText}>İlk yanıtı sen verebilirsin.</Text></View>}

    <View style={styles.answerComposer}>
      <View style={styles.answerComposerTop}><View style={styles.composerAvatar}><Text style={styles.composerAvatarText}>{username?.charAt(0).toLocaleUpperCase('tr-TR') ?? '?'}</Text></View><View style={styles.headerCopy}><Text style={styles.answerComposerTitle}>{username ? `${username} olarak yanıtla` : 'Yanıt vermek için giriş yap'}</Text><Text style={styles.answerComposerHint}>Deneyimini nazik ve anlaşılır biçimde paylaş.</Text></View></View>
      <TextInput value={answerText} onChangeText={value => { setAnswerText(value.slice(0, 5000)); setSubmitError(null); }} editable={Boolean(username) && !submitting} multiline textAlignVertical="top" placeholder={username ? 'Yanıtını buraya yaz…' : 'Önce hesabına giriş yapmalısın.'} placeholderTextColor="#94898D" style={[styles.answerInput, !username && styles.answerInputDisabled]} />
      <View style={styles.answerComposerFooter}><Text style={styles.characterCount}>{answerText.length}/5000</Text><Pressable onPress={username ? submitAnswer : onLogin} disabled={submitting} style={({ pressed }) => [styles.sendAnswerButton, submitting && styles.sendAnswerDisabled, pressed && styles.pressed]}>{submitting ? <ActivityIndicator size="small" color={colors.white} /> : <><Ionicons name={username ? 'send' : 'log-in-outline'} size={17} color={colors.white} /><Text style={styles.sendAnswerText}>{username ? 'Yanıtı gönder' : 'Giriş Yap'}</Text></>}</Pressable></View>
      {submitError ? <View style={styles.submitError}><Ionicons name="alert-circle-outline" size={16} color="#9B463B" /><Text style={styles.submitErrorText}>{submitError}</Text></View> : null}
    </View>
  </View>;
}

function plainText(value: string) { return value.replace(/<[^>]*>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim(); }
function formatDate(value: string) { return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(value)); }

const statusInset = Platform.OS === 'android' ? NativeStatusBar.currentHeight ?? 24 : 50;
const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background }, content: { paddingTop: statusInset + 12, paddingHorizontal: 19, paddingBottom: 116, width: '100%', maxWidth: 760, alignSelf: 'center' }, contentWithFloatingAction: { paddingBottom: 180 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12 }, headerIcon: { width: 54, height: 54, borderRadius: 27, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  headerCopy: { flex: 1 }, title: { color: colors.text, fontFamily: 'serif', fontSize: 30, fontWeight: '700', letterSpacing: -0.5 }, subtitle: { color: colors.muted, fontSize: 12, marginTop: 2 },
  accountButton: { maxWidth: 125, minHeight: 42, flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.lilacSoft, borderRadius: 21, paddingLeft: 5, paddingRight: 10 },
  accountAvatar: { width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary }, accountAvatarText: { color: colors.white, fontSize: 13, fontWeight: '900' }, accountText: { flexShrink: 1, color: colors.primary, fontSize: 11, fontWeight: '900' },
  tabs: { flexDirection: 'row', backgroundColor: '#EEE8E9', borderRadius: 20, padding: 5, marginTop: 24, marginBottom: 18 }, tab: { flex: 1, minHeight: 45, borderRadius: 16, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 3 },
  activeTab: { backgroundColor: colors.card, ...shadow }, tabText: { color: colors.muted, fontSize: 11, fontWeight: '800' }, activeTabText: { color: colors.primary },
  composer: { flexDirection: 'row', alignItems: 'center', gap: 11, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 21, padding: 13, ...shadow },
  composerAvatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' }, composerCopy: { flex: 1 },
  composerAvatarText: { color: colors.primary, fontSize: 16, fontWeight: '900' },
  composerTitle: { color: colors.text, fontSize: 14, fontWeight: '900' }, composerText: { color: colors.muted, fontSize: 10, marginTop: 3 }, photoButton: { width: 39, height: 39, borderRadius: 20, backgroundColor: colors.sageSoft, alignItems: 'center', justifyContent: 'center' },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 28, marginBottom: 14 }, sectionTitle: { color: colors.text, fontFamily: 'serif', fontSize: 23, fontWeight: '700' }, sectionHint: { color: colors.muted, fontSize: 10, marginTop: 3 },
  addButton: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: colors.primary, borderRadius: 17, paddingHorizontal: 13, minHeight: 38 }, addText: { color: colors.white, fontSize: 11, fontWeight: '800' },
  postsLoading: { minHeight: 90, alignItems: 'center', justifyContent: 'center', gap: 8 }, postsLoadingText: { color: colors.muted, fontSize: 11 },
  postsError: { backgroundColor: colors.peachSoft, borderRadius: 16, padding: 14, marginBottom: 14 }, postsErrorText: { color: '#8D4339', fontSize: 11 },
  questionIntro: { flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: colors.lilacSoft, borderRadius: 21, padding: 15, marginTop: 4, marginBottom: 14 }, questionIntroIcon: { width: 45, height: 45, borderRadius: 23, backgroundColor: colors.card, alignItems: 'center', justifyContent: 'center' },
  questionIntroTitle: { color: colors.text, fontFamily: 'serif', fontSize: 19, fontWeight: '700' }, questionIntroText: { color: colors.muted, fontSize: 10, lineHeight: 15, marginTop: 3 },
  questionCount: { minWidth: 48, alignItems: 'center' }, questionCountValue: { color: colors.primary, fontSize: 18, fontWeight: '900' }, questionCountLabel: { color: colors.muted, fontSize: 9, marginTop: 1 },
  questionSearch: { minHeight: 50, flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 15, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: 17 }, questionSearchInput: { flex: 1, color: colors.text, fontSize: 13, paddingVertical: 12 },
  categoryList: { gap: 8, paddingVertical: 13, paddingRight: 16 }, categoryChip: { minHeight: 34, justifyContent: 'center', paddingHorizontal: 13, borderRadius: 17, backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border }, categoryChipActive: { backgroundColor: colors.primary, borderColor: colors.primary }, categoryChipText: { color: colors.muted, fontSize: 10, fontWeight: '800' }, categoryChipTextActive: { color: colors.white },
  questionToolbar: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 13, gap: 8 }, resultText: { color: colors.muted, fontSize: 10, fontWeight: '700' }, sortGroup: { flexDirection: 'row', gap: 6 }, sortButton: { paddingHorizontal: 10, paddingVertical: 7, borderRadius: 13, backgroundColor: '#EEE8E9' }, sortButtonActive: { backgroundColor: colors.yellowSoft }, sortText: { color: colors.muted, fontSize: 9, fontWeight: '800' }, sortTextActive: { color: '#72530D' },
  questionGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between' }, questionCard: { width: '100%', backgroundColor: colors.card, borderRadius: 20, padding: 17, borderWidth: 1, borderColor: colors.border, marginBottom: 12, ...shadow }, questionCardWide: { width: '49%' }, questionCardTop: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  questionCategory: { color: '#4E7458', backgroundColor: colors.sageSoft, alignSelf: 'flex-start', paddingHorizontal: 9, paddingVertical: 4, borderRadius: 11, overflow: 'hidden', fontSize: 9, fontWeight: '900' },
  solvedBadge: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: colors.sageSoft, borderRadius: 11, paddingHorizontal: 7, paddingVertical: 4 }, solvedText: { color: '#41604A', fontSize: 8, fontWeight: '900' }, questionTitle: { color: colors.text, fontSize: 15, lineHeight: 21, fontWeight: '900', marginTop: 11 }, questionExcerpt: { color: colors.muted, fontSize: 11, lineHeight: 17, marginTop: 7 },
  authorRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 14 }, authorAvatar: { width: 29, height: 29, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft }, authorLetter: { color: colors.primary, fontSize: 11, fontWeight: '900' }, authorCopy: { flex: 1 }, authorName: { color: colors.text, fontSize: 10, fontWeight: '800' },
  questionFooter: { flexDirection: 'row', alignItems: 'center', gap: 13, marginTop: 13, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.border }, questionMeta: { color: colors.muted, fontSize: 9 }, metric: { flexDirection: 'row', alignItems: 'center', gap: 5 }, metricText: { color: colors.muted, fontSize: 9, fontWeight: '700' },
  questionState: { minHeight: 150, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card, borderRadius: 20, padding: 20, gap: 7, marginBottom: 12 }, stateTitle: { color: colors.text, fontSize: 13, fontWeight: '900' }, stateText: { color: colors.muted, textAlign: 'center', fontSize: 10, lineHeight: 15 }, retryButton: { backgroundColor: colors.lilacSoft, borderRadius: 14, paddingHorizontal: 14, paddingVertical: 9, marginTop: 3 }, retryText: { color: colors.primary, fontSize: 10, fontWeight: '900' },
  detailBack: { alignSelf: 'flex-start', flexDirection: 'row', alignItems: 'center', gap: 7, paddingVertical: 9, paddingRight: 12, marginBottom: 8 }, detailBackText: { color: colors.primary, fontSize: 11, fontWeight: '900' }, detailQuestion: { backgroundColor: colors.card, borderRadius: 22, padding: 19, borderWidth: 1, borderColor: colors.border, ...shadow }, detailTitle: { color: colors.text, fontFamily: 'serif', fontSize: 23, lineHeight: 29, fontWeight: '700', marginTop: 13 }, detailBody: { color: colors.muted, fontSize: 12, lineHeight: 20, marginTop: 12 }, detailMeta: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 6, marginTop: 17, paddingTop: 14, borderTopWidth: 1, borderTopColor: colors.border }, metaDot: { color: colors.border },
  answersHeading: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 22, marginBottom: 12 }, answersTitle: { color: colors.text, fontFamily: 'serif', fontSize: 22, fontWeight: '700' }, answersCount: { color: colors.primary, backgroundColor: colors.lilacSoft, borderRadius: 12, paddingHorizontal: 8, paddingVertical: 3, overflow: 'hidden', fontSize: 10, fontWeight: '900' }, answerCard: { backgroundColor: colors.card, borderRadius: 19, padding: 16, borderWidth: 1, borderColor: colors.border, marginBottom: 11 }, acceptedAnswer: { borderColor: colors.sage, backgroundColor: '#FBFFF9' }, answerTop: { flexDirection: 'row', alignItems: 'center', gap: 9 }, answerBody: { color: colors.text, fontSize: 11, lineHeight: 18, marginTop: 12 }, answerScore: { flexDirection: 'row', alignItems: 'center', gap: 5, marginTop: 12 },
  answerComposer: { backgroundColor: colors.card, borderRadius: 22, borderWidth: 1, borderColor: '#D8C1E8', padding: 16, marginTop: 17, ...shadow }, answerComposerTop: { flexDirection: 'row', alignItems: 'center', gap: 10 }, answerComposerTitle: { color: colors.text, fontSize: 13, fontWeight: '900' }, answerComposerHint: { color: colors.muted, fontSize: 9, marginTop: 3 }, answerInput: { minHeight: 115, color: colors.text, backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border, borderRadius: 16, padding: 13, fontSize: 12, lineHeight: 18, marginTop: 14 }, answerInputDisabled: { opacity: 0.65 }, answerComposerFooter: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10, marginTop: 10 }, characterCount: { color: colors.muted, fontSize: 9 }, sendAnswerButton: { minWidth: 132, minHeight: 43, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, backgroundColor: colors.primary, borderRadius: 15, paddingHorizontal: 14 }, sendAnswerDisabled: { opacity: 0.6 }, sendAnswerText: { color: colors.white, fontSize: 10, fontWeight: '900' }, submitError: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: colors.peachSoft, borderRadius: 12, padding: 10, marginTop: 10 }, submitErrorText: { flex: 1, color: '#823D35', fontSize: 9, lineHeight: 14 },
  floatingAskButton: { position: 'absolute', bottom: Platform.OS === 'ios' ? 96 : 88, minWidth: 166, minHeight: 52, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, paddingHorizontal: 19, borderRadius: 26, backgroundColor: colors.primary, borderWidth: 2, borderColor: '#FFFCF8', shadowColor: '#4D3D45', shadowOffset: { width: 0, height: 8 }, shadowOpacity: 0.24, shadowRadius: 15, elevation: 9 }, floatingAskText: { color: colors.white, fontSize: 12, fontWeight: '900' }, pressed: { opacity: 0.8, transform: [{ scale: 0.985 }] },
});
