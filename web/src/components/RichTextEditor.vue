<script setup lang="ts">
import { onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { Editor, Toolbar } from '@wangeditor/editor-for-vue'
import '@wangeditor/editor/dist/css/style.css'

/**
 * 富文本编辑器（wangEditor v5 封装）：v-model 双向绑定 HTML。
 * 注意：编辑器须在可见容器内初始化，父级用 v-if 控制创建时机（如弹窗打开后）。
 */
const props = defineProps<{ modelValue: string; placeholder?: string; height?: number }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const editorRef = shallowRef()
const editorHeight = (props.height ?? 300) + 'px'
const htmlValue = ref(props.modelValue)

watch(
  () => props.modelValue,
  (v) => {
    // 外部重置（如打开弹窗回填）时同步进编辑器
    if (v !== htmlValue.value) htmlValue.value = v
  }
)

const toolbarConfig = {
  excludeKeys: ['group-video', 'insertVideo', 'uploadVideo', 'fullScreen']
}

const editorConfig = {
  placeholder: props.placeholder ?? '请输入内容…',
  MENU_CONF: {
    uploadImage: {
      // 内网系统：图片以 base64 内嵌，无需文件服务
      base64LimitSize: 10 * 1024 * 1024
    }
  }
}

function handleCreated(editor: any): void {
  editorRef.value = editor
}

function handleChange(): void {
  emit('update:modelValue', htmlValue.value)
}

onBeforeUnmount(() => {
  editorRef.value?.destroy?.()
})
</script>

<template>
  <div class="rich-editor">
    <Toolbar class="editor-toolbar" :editor="editorRef" :default-config="toolbarConfig" mode="default" />
    <Editor
      v-model="htmlValue"
      class="editor-content"
      :style="{ height: editorHeight, overflowY: 'auto' }"
      :default-config="editorConfig"
      mode="default"
      @on-created="handleCreated"
      @on-change="handleChange"
    />
  </div>
</template>

<style scoped>
.rich-editor {
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  z-index: 2; /* 工具栏下拉需盖过页面元素 */
  background: var(--el-bg-color);
}

.editor-toolbar {
  border-bottom: 1px solid var(--el-border-color-lighter);
}


</style>
