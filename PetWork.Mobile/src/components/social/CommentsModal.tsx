import { Ionicons } from '@expo/vector-icons';
import { useCallback, useEffect, useState } from 'react';
import {
  ActivityIndicator, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, TextInput, View,
} from 'react-native';
import { createSocialComment, getSocialComments, setSocialCommentLike, type SocialCommentPayload } from '../../api';
import { colors, shadow } from '../../theme';
import type { SocialPost } from '../../types/social';

type Props = {
  visible: boolean;
  post: SocialPost | null;
  token: string | null;
  username: string | null;
  onClose: () => void;
  onLogin: () => void;
  onCommentAdded: (postId: number) => void;
};

export function CommentsModal({ visible, post, token, username, onClose, onLogin, onCommentAdded }: Props) {
  const [comments, setComments] = useState<SocialCommentPayload[]>([]);
  const [body, setBody] = useState('');
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [likingId, setLikingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadComments = useCallback(async (signal?: AbortSignal) => {
    if (!post?.serverId) return;
    setLoading(true);
    setError(null);
    try {
      setComments(await getSocialComments(post.serverId, token, signal));
    } catch (reason) {
      if (!signal?.aborted) setError(reason instanceof Error ? reason.message : 'Yorumlar yüklenemedi.');
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, [post?.serverId, token]);

  useEffect(() => {
    if (!visible || !post?.serverId) return;
    const controller = new AbortController();
    loadComments(controller.signal);
    return () => controller.abort();
  }, [visible, post?.serverId, loadComments]);

  useEffect(() => {
    if (!visible) {
      setBody('');
      setComments([]);
      setError(null);
    }
  }, [visible]);

  const submit = async () => {
    const commentBody = body.trim();
    if (!post?.serverId || commentBody.length === 0) return;
    if (!token || !username) {
      onClose();
      onLogin();
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const created = await createSocialComment(token, post.serverId, commentBody);
      setComments(current => [...current, created]);
      setBody('');
      onCommentAdded(post.serverId);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Yorum gönderilemedi.');
    } finally {
      setSubmitting(false);
    }
  };

  const toggleLike = async (comment: SocialCommentPayload) => {
    if (!token || !username) {
      onClose();
      onLogin();
      return;
    }
    if (likingId !== null) return;
    setLikingId(comment.id);
    setError(null);
    try {
      const result = await setSocialCommentLike(token, comment.id, !comment.isLikedByMe);
      setComments(current => current.map(item => item.id === comment.id
        ? { ...item, isLikedByMe: result.active, likeCount: result.count }
        : item));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Yorum beğenisi kaydedilemedi.');
    } finally {
      setLikingId(null);
    }
  };

  return <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
    <KeyboardAvoidingView style={styles.overlay} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Pressable style={styles.backdrop} onPress={submitting ? undefined : onClose} />
      <View style={styles.sheet}>
        <View style={styles.handle} />
        <View style={styles.header}>
          <View style={styles.headerCopy}><Text style={styles.title}>Yorumlar</Text><Text style={styles.subtitle} numberOfLines={1}>{post?.body}</Text></View>
          <Pressable onPress={onClose} disabled={submitting} style={styles.closeButton}><Ionicons name="close" size={22} color={colors.primary} /></Pressable>
        </View>

        <ScrollView style={styles.list} contentContainerStyle={styles.listContent} keyboardShouldPersistTaps="handled">
          {loading ? <View style={styles.center}><ActivityIndicator color={colors.primary} /><Text style={styles.helper}>Yorumlar yükleniyor…</Text></View> : null}
          {!loading && comments.length === 0 && !error ? <View style={styles.empty}><Ionicons name="chatbubbles-outline" size={34} color={colors.primary} /><Text style={styles.emptyTitle}>Henüz yorum yok</Text><Text style={styles.helper}>İlk gerçek yorumu sen yazabilirsin.</Text></View> : null}
          {comments.map(comment => <View key={comment.id} style={styles.comment}>
            <View style={styles.avatar}><Text style={styles.avatarText}>{comment.username.charAt(0).toLocaleUpperCase('tr-TR')}</Text></View>
            <View style={styles.commentBody}>
              <View style={styles.nameRow}><Text style={styles.username}>@{comment.username}</Text>{comment.isAdmin ? <Text style={styles.adminBadge}>Yönetici</Text> : null}</View>
              <Text style={styles.commentText}>{comment.body}</Text>
              <View style={styles.commentFooter}>
                <Text style={styles.date}>{new Date(comment.createdAt).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' })}</Text>
                <Pressable disabled={likingId !== null} onPress={() => void toggleLike(comment)} accessibilityLabel={comment.isLikedByMe ? 'Yorum beğenisini kaldır' : 'Yorumu beğen'} style={styles.likeButton}>
                  <Ionicons name={comment.isLikedByMe ? 'heart' : 'heart-outline'} size={16} color={comment.isLikedByMe ? colors.danger : colors.primary} />
                  {comment.likeCount > 0 ? <Text style={[styles.likeCount, comment.isLikedByMe && styles.likeCountActive]}>{comment.likeCount}</Text> : null}
                </Pressable>
              </View>
            </View>
          </View>)}
        </ScrollView>

        {error ? <Pressable onPress={() => loadComments()} style={styles.errorBox}><Text style={styles.errorText}>{error}</Text><Text style={styles.retry}>Yeniden dene</Text></Pressable> : null}

        <View style={styles.composer}>
          <TextInput
            value={body}
            onChangeText={setBody}
            placeholder={username ? `@${username} olarak yorum yaz…` : 'Yorum yapmak için giriş yap…'}
            placeholderTextColor="#94898D"
            maxLength={1000}
            multiline
            style={styles.input}
          />
          <Pressable onPress={submit} disabled={submitting || body.trim().length === 0} style={[styles.sendButton, (submitting || body.trim().length === 0) && styles.disabled]}>
            {submitting ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="send" size={19} color={colors.white} />}
          </Pressable>
        </View>
        <Text style={styles.xpNote}>Yorum yapmak hesabına 5 XP kazandırır.</Text>
      </View>
    </KeyboardAvoidingView>
  </Modal>;
}

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'flex-end' },
  backdrop: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: '#241B2188' },
  sheet: { height: '82%', backgroundColor: colors.background, borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingTop: 10, paddingBottom: Platform.OS === 'ios' ? 28 : 18, ...shadow },
  handle: { width: 42, height: 5, borderRadius: 3, backgroundColor: '#D8CDD1', alignSelf: 'center', marginBottom: 14 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 20, paddingBottom: 14, borderBottomWidth: 1, borderBottomColor: colors.border },
  headerCopy: { flex: 1 }, title: { color: colors.text, fontFamily: 'serif', fontSize: 24, fontWeight: '700' }, subtitle: { color: colors.muted, fontSize: 10, marginTop: 3 },
  closeButton: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.lilacSoft, alignItems: 'center', justifyContent: 'center' },
  list: { flex: 1 }, listContent: { paddingHorizontal: 20, paddingVertical: 15, flexGrow: 1 }, center: { minHeight: 140, alignItems: 'center', justifyContent: 'center', gap: 9 },
  empty: { flex: 1, minHeight: 180, alignItems: 'center', justifyContent: 'center' }, emptyTitle: { color: colors.text, fontSize: 15, fontWeight: '900', marginTop: 10 }, helper: { color: colors.muted, fontSize: 10, marginTop: 5 },
  comment: { flexDirection: 'row', alignItems: 'flex-start', gap: 10, marginBottom: 15 }, avatar: { width: 38, height: 38, borderRadius: 19, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.lilacSoft }, avatarText: { color: colors.primary, fontSize: 14, fontWeight: '900' },
  commentBody: { flex: 1, backgroundColor: colors.card, borderRadius: 17, padding: 12, borderWidth: 1, borderColor: colors.border }, nameRow: { flexDirection: 'row', alignItems: 'center', gap: 7 }, username: { color: colors.text, fontSize: 11, fontWeight: '900' }, adminBadge: { color: '#4E7458', backgroundColor: colors.sageSoft, borderRadius: 9, paddingHorizontal: 6, paddingVertical: 2, overflow: 'hidden', fontSize: 7, fontWeight: '900' },
  commentText: { color: colors.text, fontSize: 12, lineHeight: 18, marginTop: 6 },
  commentFooter: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 7 },
  date: { color: colors.muted, fontSize: 8 }, likeButton: { minWidth: 32, minHeight: 28, flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-end', gap: 4 },
  likeCount: { color: colors.muted, fontSize: 9, fontWeight: '800' }, likeCountActive: { color: colors.danger },
  errorBox: { marginHorizontal: 20, marginBottom: 10, backgroundColor: colors.peachSoft, borderRadius: 14, padding: 11 }, errorText: { color: '#8D4339', fontSize: 10 }, retry: { color: colors.primary, fontSize: 10, fontWeight: '900', marginTop: 5 },
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: 9, paddingHorizontal: 20, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.border }, input: { flex: 1, minHeight: 48, maxHeight: 110, color: colors.text, backgroundColor: colors.card, borderRadius: 17, borderWidth: 1, borderColor: colors.border, paddingHorizontal: 14, paddingVertical: 12, fontSize: 12 },
  sendButton: { width: 48, height: 48, borderRadius: 24, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary }, disabled: { opacity: 0.45 }, xpNote: { color: colors.muted, textAlign: 'center', fontSize: 8, marginTop: 7 },
});
